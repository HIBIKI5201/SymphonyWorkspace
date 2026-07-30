using SymphonyFrameWork.Core;
using SymphonyFrameWork.Debugger.Logger;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     フォルダを生成する
    /// </summary>
    public static class FolderGenerator
    {
        /// <summary>
        ///     規定のディレクトリ構成を生成する
        /// </summary>
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + nameof(FolderGenerator), priority = 100)]
        public static void GenerateFolder()
        {
            SymphonyDebugLogger.NewText($"[{nameof(GenerateFolder)}]");

            string[] assetsFolders = LoadFolderPaths(STRUCTURE_PATH);

            //全てのフォルダを生成する
            foreach (string folder in assetsFolders)
            {
                string path = $"{ASSETS_PATH}/{folder}";

                if (!AssetDatabase.IsValidFolder(path))
                {
                    FolderCreate(path);
                    SymphonyDebugLogger.AddText($"フォルダ作成: {path}");
                }
            }
            AssetDatabase.Refresh();

            SymphonyDebugLogger.LogText();
            EditorUtility.DisplayDialog("フォルダを生成", "フォルダを生成しました", "OK");
        }

        private const string ASSETS_PATH = "Assets";
        private static readonly string STRUCTURE_PATH = EditorSymphonyConstant.FRAMEWORK_PATH + "/Editor/Generator/FolderGenerate/FolderStructure.md";

        /// <summary>
        ///     パスのフォルダを生成する。
        /// </summary>
        /// <param name="path"> 再帰的に生成するAssets配下のフォルダパス。 </param>
        private static void FolderCreate(string path)
        {
            // フォルダがあれば終了。
            if (AssetDatabase.IsValidFolder(path)) return;

            // パスをディレクトリとフォルダ名に分ける。
            string parent = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);

            // ディレクトリが無ければ再帰的にフォルダを生成する。
            if (!AssetDatabase.IsValidFolder(parent))
            {
                FolderCreate(parent);
            }

            // パスのフォルダを作成。
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>
        ///     全てのフォルダのパスを生成して返す。
        /// </summary>
        /// <param name="markdownPath"> フォルダ構成を記述したMarkdownファイルのパス。 </param>
        /// <returns> Assetsからの相対フォルダパス一覧。 </returns>
        public static string[] LoadFolderPaths(string markdownPath)
        {
            string[] lines = File.ReadAllLines(markdownPath);

            List<string> result = new();
            Stack<string> stack = new();

            foreach (var rawLine in lines)
            {
                int dashIndex = rawLine.IndexOf('-');

                if (dashIndex < 0) { continue; }

                int depth = dashIndex / 2;

                string folder = rawLine[(dashIndex + 1)..].Trim();

                while (stack.Count > depth)
                {
                    stack.Pop();
                }

                stack.Push(folder);

                string path = string.Join("/", stack.Reverse());

                result.Add(path);
            }

            return result.ToArray();
        }
    }
}
