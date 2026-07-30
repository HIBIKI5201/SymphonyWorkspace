using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using static SymphonyFrameWork.Editor.AssetStoreToolsPackager;

namespace SymphonyFrameWork.Editor
{
    /// <summary> Asset Store Toolsの出力対象とパッケージ形式を選択するEditorWindow。 </summary>
    public sealed class AssetStoreToolsPackageWindow : EditorWindow
    {
        /// <summary> 設定済みパスを検証してPackagerウィンドウを表示する。 </summary>
        public static void ShowWindow()
        {
            string assetStoreToolsPath = AssetStoreToolsPackagerData.AssetStoreToolsPath;

            if (string.IsNullOrEmpty(assetStoreToolsPath))
            {
                Debug.LogError("AssetStoreToolsフォルダのパスが設定されていません。");
                return;
            }

            // パッケージ対象ディレクトリをバリデーションチェック。
            if (!AssetDatabase.IsValidFolder(assetStoreToolsPath))
            {
                Debug.LogError($"AssetStoreToolsフォルダが存在しません: {assetStoreToolsPath}");
                return;
            }


            GetWindow<AssetStoreToolsPackageWindow>(false, "Asset Store Tools Packager", true);
        }

        private sealed class DirectoryItem
        {
            /// <summary> パッケージ対象ディレクトリのパス。 </summary>
            public string Path;

            /// <summary> UIへ表示するディレクトリ名。 </summary>
            public string Name;

            /// <summary> ユーザーが出力対象として選択しているかを示す。 </summary>
            public bool IsSelected;

            /// <summary> 除外設定により選択できないかを示す。 </summary>
            public bool IsIgnored;
        }

        private List<DirectoryItem> _directoryItems = new();
        private Vector2 _scrollPosition;
        private PackageMode _packageMode = PackageMode.Singles;
        private bool _createZip = false;
        private bool _usedDependencies = false;

        /// <summary> ウィンドウ有効化時に出力対象ディレクトリ一覧を読み込む。 </summary>
        private void OnEnable()
        {
            RefreshDirectories();
        }

        /// <summary> ディレクトリ選択、出力形式、エクスポート操作を描画する。 </summary>
        private void OnGUI()
        {
            GUILayout.Label("Asset Store Tools Packager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            {
                RefreshDirectories();
            }

            EditorGUILayout.Space();

            // 一括選択・解除。
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                _directoryItems.Where(d => !d.IsIgnored).ToList().ForEach(d => d.IsSelected = true);
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                _directoryItems.ForEach(d => d.IsSelected = false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ディレクトリ一覧。
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, EditorStyles.helpBox);
            foreach (DirectoryItem item in _directoryItems)
            {
                using (new EditorGUI.DisabledGroupScope(item.IsIgnored))
                {
                    item.IsSelected = EditorGUILayout.ToggleLeft(item.IsIgnored ? $"{item.Name} (Ignored)" : item.Name, item.IsSelected);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            _packageMode = (PackageMode)EditorGUILayout.EnumFlagsField("Export Mode", _packageMode);
            _createZip = EditorGUILayout.ToggleLeft("Create ZIP File", _createZip);
            _usedDependencies = EditorGUILayout.ToggleLeft("Used Dependencies", _usedDependencies);

            // エクスポートボタン。
            using (new EditorGUI.DisabledGroupScope(_directoryItems.All(d => !d.IsSelected)))
            {
                if (_packageMode == PackageMode.Nothing)
                {
                    GUILayout.TextField("Noting mode is invalid");
                    return;
                }

                if (GUILayout.Button("Export Selected Directories", GUILayout.Height(30)))
                {
                    string[] selectedDirs = _directoryItems
                        .Where(d => d.IsSelected)
                        .Select(d => d.Path)
                        .ToArray();

                    Export(selectedDirs,
                        _packageMode,
                        _createZip,
                        _usedDependencies);
                }
            }
        }

        /// <summary>
        ///     ディレクトリ一覧をリフレッシュして、AssetStoreToolsPackagerから情報を取得する。
        /// </summary>
        private void RefreshDirectories()
        {
            _directoryItems.Clear();

            var infos = GetPackageDirectories();
            foreach (var info in infos)
            {
                _directoryItems.Add(new DirectoryItem
                {
                    Path = info.Path,
                    Name = info.Name,
                    IsSelected = !info.IsIgnored,
                    IsIgnored = info.IsIgnored
                });
            }
        }
    }
}
