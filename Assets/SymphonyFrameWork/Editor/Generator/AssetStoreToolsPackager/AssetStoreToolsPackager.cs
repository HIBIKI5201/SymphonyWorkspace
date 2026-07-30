using SymphonyFrameWork.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     AssetStoreToolsフォルダをパッケージ化するクラス。
    /// </summary>
    public static class AssetStoreToolsPackager
    {
        /// <summary> パッケージ候補ディレクトリの表示名、パス、除外状態を保持する。 </summary>
        public sealed class PackageDirectoryInfo
        {
            /// <summary> パッケージ対象ディレクトリのパス。 </summary>
            public string Path;

            /// <summary> UIへ表示するディレクトリ名。 </summary>
            public string Name;

            /// <summary> 除外設定に含まれているかを示す。 </summary>
            public bool IsIgnored;
        }

        /// <summary> 個別または統合パッケージの出力形式を表すフラグ。 </summary>
        [Flags]
        public enum PackageMode : byte
        {
            /// <summary> パッケージを出力しない無効な状態。 </summary>
            Nothing = 0,

            /// <summary> 選択ディレクトリを個別のパッケージとして出力する。 </summary>
            Singles = 1 << 0,

            /// <summary> 選択ディレクトリを1つの統合パッケージとして出力する。 </summary>
            Combine = 1 << 1,
        }

        /// <summary>
        ///     AssetStoreToolsフォルダをパッケージ化してExportedPackagesフォルダに保存します。
        /// </summary>
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + nameof(ExportAssetStoreToolsFolder), priority = 100)]
        public static void ExportAssetStoreToolsFolder()
        {
            AssetStoreToolsPackageWindow.ShowWindow();
        }

        /// <summary>
        ///     パッケージ対象のディレクトリ一覧を取得します。無視リストのフィルタリングもここで行います。
        /// </summary>
        public static IReadOnlyList<PackageDirectoryInfo> GetPackageDirectories()
        {
            List<PackageDirectoryInfo> results = new();

            if (!AssetDatabase.IsValidFolder(AssetStoreToolsPackagerData.AssetStoreToolsPath))
            {
                return results;
            }

            // 無視ファイルの確認と作成。
            HashSet<string> ignoredNames = GetIgnoredNames();

            // ディレクトリの取得と情報の生成。
            string[] dirs = Directory.GetDirectories(AssetStoreToolsPackagerData.AssetStoreToolsPath);
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                bool isIgnored = ignoredNames.Contains(name);

                results.Add(new PackageDirectoryInfo
                {
                    Path = dir.Replace("\\", "/"),
                    Name = name,
                    IsIgnored = isIgnored
                });
            }

            return results;
        }

        /// <summary> 指定ディレクトリを選択された形式で出力し、必要に応じてZIP化する。 </summary>
        public static void Export(string[] directories, PackageMode mode, bool createZip = false, bool usedDependencies = false)
        {
            if (directories.Length == 0)
            {
                Debug.LogWarning("パッケージ化するフォルダが存在しませんでした。");
                return;
            }

            var context = new AssetStoreToolsPackageContext(
                PACKAGE_NAME,
                AssetStoreToolsPackagerData.ExportedPackagesPath,
                directories
            );

            // 出力フォルダ作成
            if (!Directory.Exists(context.ExportFullPath))
            {
                Directory.CreateDirectory(context.ExportFullPath);
            }


            HashSet<string> usedAssetPaths = null;
            if (usedDependencies)
            {
                string astPath = AssetStoreToolsPackagerData.AssetStoreToolsPath;
                usedAssetPaths = GetProjectUsedDependencies(astPath);
            }

            if ((mode & PackageMode.Singles) != 0)
            {
                ExportPackage(context, usedAssetPaths);
            }

            if ((mode & PackageMode.Combine) != 0)
            {
                CreateCombinedPackage(context, usedAssetPaths);
            }

            if (createZip)
            {
                CreateZip(context);
            }

            Debug.Log($"[{nameof(AssetStoreToolsPackager)}]\nパッケージを出力しました\npath : {context.ExportLocalPath}");
        }


        private const string PACKAGE_NAME = "AssetStoreToolsPackage";

        /// <summary>
        ///     個別のパッケージ生成。
        /// </summary>
        /// <param name="context"> 出力対象と出力先を保持するパッケージコンテキスト。 </param>
        /// <param name="usedAssetPaths"> 使用中アセットだけを出力する場合のパス集合。 </param>
        private static void ExportPackage(
            AssetStoreToolsPackageContext context,
            HashSet<string> usedAssetPaths = null)
        {
            foreach (string dir in context.ExportDirectories)
            {
                try
                {
                    string[] exportFiles;
                    ExportPackageOptions options;

                    if (usedAssetPaths != null)
                    {
                        exportFiles = GetUsedAssetsInDirectory(dir, usedAssetPaths);
                        options = ExportPackageOptions.Default;

                        if (exportFiles.Length == 0)
                        {
                            Debug.LogWarning($"使用中アセットなし: {dir}");
                            continue;
                        }
                    }
                    else
                    {
                        exportFiles = new[] { dir };
                        options = ExportPackageOptions.Recurse;
                    }

                    AssetDatabase.ExportPackage(
                        exportFiles,
                        Path.Combine(
                            context.ExportLocalPath,
                            $"{Path.GetFileName(dir)}.unitypackage"),
                        options
                    );
                }
                catch (Exception e)
                {
                    Debug.LogError($"パッケージの出力に失敗しました: {dir}\n{e}");
                }
            }
        }

        /// <summary>
        ///     連結されたパッケージ生成。
        /// </summary>
        /// <param name="context"> 出力対象と出力先を保持するパッケージコンテキスト。 </param>
        /// <param name="usedAssetPaths"> 使用中アセットだけを出力する場合のパス集合。 </param>
        private static void CreateCombinedPackage(
            in AssetStoreToolsPackageContext context,
            HashSet<string> usedAssetPaths = null)
        {
            try
            {
                string combinedName =
                    $"AllPackages_{context.DateTime:yyyyMMdd_HHmmss}.unitypackage";

                string[] exportFiles;
                ExportPackageOptions options;

                if (usedAssetPaths != null)
                {
                    exportFiles = context.ExportDirectories
                        .SelectMany(dir => GetUsedAssetsInDirectory(dir, usedAssetPaths))
                        .Distinct()
                        .ToArray();
                    options = ExportPackageOptions.Default;

                    if (exportFiles.Length == 0)
                    {
                        Debug.LogWarning("使用中アセットが存在しないため統合パッケージを作成しませんでした。");
                        return;
                    }
                }
                else
                {
                    exportFiles = context.ExportDirectories;
                    options = ExportPackageOptions.Recurse;
                }

                AssetDatabase.ExportPackage(
                    exportFiles,
                    Path.Combine(context.ExportLocalPath, combinedName),
                    options
                );

                Debug.Log($"合成パッケージ作成: {combinedName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"合計パッケージの出力に失敗\n{e}");
            }
        }

        /// <summary>
        ///     指定フォルダをZIP化する
        /// </summary>
        /// <param name="context"> 圧縮対象と出力先を保持するパッケージコンテキスト。 </param>
        private static void CreateZip(in AssetStoreToolsPackageContext context)
        {
            try
            {
                string zipFullPath = Path.Combine(context.ExportRoot, $"{context.PackageName}.zip");

                if (!Directory.Exists(context.ExportFullPath))
                {
                    Debug.LogError($"ZIP対象フォルダが存在しません: {context.ExportFullPath}");
                    return;
                }

                if (File.Exists(zipFullPath))
                {
                    File.Delete(zipFullPath);
                }

                ZipFile.CreateFromDirectory(
                    context.ExportFullPath,
                    zipFullPath,
                    CompressionLevel.Optimal,
                    true
                );

                Debug.Log($"ZIP作成完了: {zipFullPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ZIP作成失敗\n{e}");
            }
        }

        /// <summary> 除外設定ファイルを保証し、コメントと空行を除いたフォルダ名を取得する。 </summary>
        private static HashSet<string> GetIgnoredNames()
        {
            HashSet<string> ignoredNames = new HashSet<string>();
            if (!File.Exists(EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE))
            {
                File.WriteAllText(EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE, "# Write folder names to ignore (one per line)\n");
                AssetDatabase.Refresh();
            }
            else
            {
                string[] lines = File.ReadAllLines(EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                    {
                        ignoredNames.Add(trimmed);
                    }
                }
            }
            return ignoredNames;
        }

        /// <summary>
        /// プロジェクト内の全アセット内で一つでも依存している（＝使用している）アセットのパス一覧を取得する
        /// </summary>
        private static HashSet<string> GetProjectUsedDependencies(string excludedRootPath)
        {
            HashSet<string> usedPaths = new();

            // プロジェクト内のすべての一般アセット（Assetsフォルダ以下）を検索
            string[] allAssetGuids = AssetDatabase.FindAssets("", new[] { "Assets" });

            string normalizedExcludedRootPath = excludedRootPath
                ?.Replace("\\", "/")
                .TrimEnd('/');

            foreach (string guid in allAssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // AssetStoreToolsは除外して依存関係を追う
                if (!string.IsNullOrEmpty(normalizedExcludedRootPath)
                    && (path.Equals(normalizedExcludedRootPath, StringComparison.Ordinal)
                       || path.StartsWith(normalizedExcludedRootPath + "/", StringComparison.Ordinal)))
                {
                    continue;
                }

                // そのアセットが依存しているリソースをすべて取得
                string[] dependencies = AssetDatabase.GetDependencies(path, recursive: true);

                foreach (string dependency in dependencies)
                {
                    usedPaths.Add(dependency);
                }
            }

            return usedPaths;
        }

        /// <summary>
        /// 指定ディレクトリ内で実際に使用されているアセットのみ取得する
        /// </summary>
        private static string[] GetUsedAssetsInDirectory(
            string dir,
            HashSet<string> usedAssetPaths)
        {
            string[] allFiles = Directory.GetFiles(
                    dir,
                    "*.*",
                    SearchOption.AllDirectories)
                .Select(p => p.Replace("\\", "/"))
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return allFiles
                .Where(files => usedAssetPaths.Contains(files)
                                || files.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
}
