using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     タグやレイヤーの変更を監視する
    /// </summary>
    public sealed class TagsAndLayersPostProcessor : AssetPostprocessor
    {
        /// <summary> Build Settingsのシーン一覧に対する変更通知。 </summary>
        public static TagsAndLayersSettingData SceneList = new();

        /// <summary> TagManagerのタグ設定に対する変更通知。 </summary>
        public static TagsAndLayersSettingData Tags = new("ProjectSettings/TagManager.asset");

        /// <summary> TagManagerのレイヤー設定に対する変更通知。 </summary>
        public static TagsAndLayersSettingData Layers = new("ProjectSettings/TagManager.asset");

        /// <summary> 監視対象設定ファイルの内容と変更通知を保持する。 </summary>
        public sealed class TagsAndLayersSettingData : IDisposable
        {
            private string _managerPath = string.Empty;
            /// <summary> 監視対象のProjectSettingsファイルパス。 </summary>
            public string TagManagerPath { get => _managerPath; }

            private string _lastManagerContent = string.Empty;
            /// <summary> 前回確認した設定ファイルの内容。 </summary>
            public string LastManagerContent { get => _lastManagerContent; }

            /// <summary> 監視対象の設定が変更されたときに発生する。 </summary>
            public event Action OnSettingChanged;

            /// <summary> ファイルを持たない手動通知用の監視データを生成する。 </summary>
            public TagsAndLayersSettingData() => _managerPath = string.Empty;

            /// <summary> 指定した設定ファイルの初期内容を読み込んで監視データを生成する。 </summary>
            public TagsAndLayersSettingData(string tagManagerPath)
            {
                if (File.Exists(tagManagerPath))
                {
                    _managerPath = tagManagerPath;
                    _lastManagerContent = File.ReadAllText(tagManagerPath);
                }
                else
                {
                    Debug.LogWarning($"{tagManagerPath}にアセットがありません。");
                }
            }

            /// <summary> 比較基準となる直近の設定ファイル内容を更新する。 </summary>
            public void SetLastManagerContent(string content) => _lastManagerContent = content;

            /// <summary> 設定変更イベントを通知する。 </summary>
            public void EventInvoke() => OnSettingChanged?.Invoke();

            /// <summary> 登録済みの変更通知をすべて解除する。 </summary>
            public void Dispose() => OnSettingChanged = null;
        }

        /// <summary> Build Settingsのシーン一覧変更イベントを再購読する。 </summary>
        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorBuildSettings.sceneListChanged -= SceneList.EventInvoke;
            EditorBuildSettings.sceneListChanged += SceneList.EventInvoke;
        }

        /// <summary> TagManagerアセットの内容が実際に変化した場合だけ変更を通知する。 </summary>
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var data in new TagsAndLayersSettingData[] { Tags, Layers })
            {
                foreach (string asset in importedAssets)
                {
                    if (asset == data.TagManagerPath)
                    {
                        string newContent = File.ReadAllText(data.TagManagerPath);
                        if (newContent != data.LastManagerContent)
                        {
                            data.SetLastManagerContent(newContent);
                            data.EventInvoke();
                        }
                        break;
                    }
                }
            }
        }
    }
}
