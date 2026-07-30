using SymphonyFrameWork.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using FileMode = System.IO.FileMode;
using Task = System.Threading.Tasks.Task;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     Enumを自動生成する
    /// </summary>
    public static class EnumGenerator
    {
        private static readonly Regex IdentifierRegex = new(@"^@?[a-zA-Z_][a-zA-Z0-9_]*$");
        private static readonly string[] ReservedWords = { "abstract", "as", "base", "bool", "break", "while" };

        /// <summary> 有効な識別子を抽出し、通常またはフラグ形式のenumソースを生成する。 </summary>
        public static async void EnumGenerate(string[] strings, string fileName, bool flag = false)
        {
            //重複を削除
            var hash = new HashSet<string>(new string[1] { "None" }.Concat(strings))
                .Where(s =>
                {
                    //文字列の頭文字がアルファベットではないものは除外
                    if (!IdentifierRegex.IsMatch(s))
                    {
                        Debug.LogWarning($"無効な文字で始まっているか無効な文字が含まれているため'{s}'を除外しました");
                        return false;
                    }

                    //プログラム文字を除外
                    if (ReservedWords.Contains(s)) Debug.LogWarning($"無効な文字列'{s}'を除外しました");

                    return true;
                })
                .ToHashSet();

            //ディレクトリを生成
            CreateResourcesFolder($"{EditorSymphonyConstant.ENUM_PATH}/");

            // ファイル名を生成
            var enumFilePath = GetEnumFilePath(fileName);

            // 中身を生成
            var content = !flag ? NormalEnumGenerate(fileName, hash) : FlagEnumGenerate(fileName, hash);

            // リトライ付きで書き込み
            int maxRetries = 5;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(enumFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        foreach (var line in content)
                        {
                            await writer.WriteLineAsync(line);
                        }
                    }

                    return; // 成功したら終了
                }
                catch (IOException e)
                {
                    if (attempt == maxRetries)
                    {
                        Debug.LogError($"ファイル書き込みに失敗しました（{enumFilePath}）：{e.Message}");
                        throw;
                    }

                    Debug.LogWarning($"ファイルが使用中のため再試行します（{attempt}/{maxRetries}）...");
                    await Task.Delay(500);
                }
            }

// アセット更新
            File.SetLastAccessTime(enumFilePath, DateTime.Now);
            AssetDatabase.ImportAsset(enumFilePath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"{fileName}Enumを生成しました");
        }

        /// <summary> 指定名の自動生成enumファイルパスを取得する。 </summary>
        public static string GetEnumFilePath(string fileName) => $"{EditorSymphonyConstant.ENUM_PATH}/{fileName}Enum.cs";

        /// <summary>
        ///     リソースフォルダが無ければ生成
        /// </summary>
        private static void CreateResourcesFolder(string resourcesPath)
        {
            //リソースがなければ生成
            if (!Directory.Exists(resourcesPath))
            {
                Directory.CreateDirectory(resourcesPath);
                AssetDatabase.ImportAsset(resourcesPath, ImportAssetOptions.ForceUpdate);
            }

            AssemblyGenerator.CreateEnumAssembly(
                EditorSymphonyConstant.ENUM_PATH + "/SymphonyFrameWork.Enum",
                EditorSymphonyConstant.FRAMEWORK_PATH + "/SymphonyFrameWork");

            AssetDatabase.Refresh();
        }



        /// <summary>
        ///     通常のEnumを生成する
        /// </summary>
        /// <param name="fileName"> 生成するenumの型名。 </param>
        /// <param name="hash"> 重複除去済みの列挙子名。 </param>
        /// <returns> 通常enumを構成するソース行。 </returns>
        private static IEnumerable<string> NormalEnumGenerate(string fileName, HashSet<string> hash)
        {
            //ファイルの中身を生成
            IEnumerable<string> content = new[]
            {
                "/// <summary> Symphony Frameworkが自動生成した列挙型。 </summary>\n"
                + "public enum " + fileName + "Enum : int\n{"
            };

            //Enumファイルに要素を追加していく
            content = content.Concat(hash.SelectMany((s, i) => new[]
            {
                $"    /// <summary> {s}を表す。 </summary>",
                $"    {s} = {i},"
            }));
            content = content.Append("}");

            return content;
        }

        /// <summary> Flags属性付きenumのソース行を生成する。 </summary>
        private static IEnumerable<string> FlagEnumGenerate(string fileName, HashSet<string> hash)
        {
            //ファイルの中身を生成
            IEnumerable<string> content = new[]
            {
                "using System;\n\n"
                + "/// <summary> Symphony Frameworkが自動生成したフラグ列挙型。 </summary>\n"
                + "[Flags]\npublic enum " + fileName + "Enum : int\n{"
            };

            //Enumファイルに要素を追加していく
            content = content.Concat(hash.SelectMany((s, i) => new[]
            {
                $"    /// <summary> {s}を表す。 </summary>",
                $"    {s} = 1 << {i},"
            }));
            content = content.Append("}");

            return content;
        }

        /// <summary> デバッグメニューからenum出力先とAssembly Definitionを生成する。 </summary>
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + "Debug/" + nameof(CreateResourcesFolder), priority = 1000)]
        private static void CreateResourceFolderDebug() => CreateResourcesFolder($"{EditorSymphonyConstant.ENUM_PATH}/");
    }
}
