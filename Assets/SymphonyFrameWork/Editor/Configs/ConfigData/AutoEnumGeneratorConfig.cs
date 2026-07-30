using SymphonyFrameWork.Core;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary> プロジェクト設定変更時のenum自動生成可否を保持する。 </summary>
    [FilePath(EditorSymphonyConstant.PROJCET_SETTING_FILE_PATH + nameof(AutoEnumGeneratorConfig) + ".asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class AutoEnumGeneratorConfig : ScriptableSingleton<AutoEnumGeneratorConfig>
    {
        /// <summary> Build Settings変更時にシーンenumを自動更新するかを示す。 </summary>
        public bool AutoSceneListUpdate
        {
            get => _autoSceneListUpdate;
            set
            {
                _autoSceneListUpdate = value;
                Save();
            }
        }

        /// <summary> タグ変更時にタグenumを自動更新するかを示す。 </summary>
        public bool AutoTagsUpdate
        {
            get => _autoTagsUpdate;
            set
            {
                _autoTagsUpdate = value;
                Save();
            }
        }

        /// <summary> レイヤー変更時にレイヤーenumを自動更新するかを示す。 </summary>
        public bool AutoLayerUpdate
        {
            get => _autoLayersUpdate;
            set
            {
                _autoLayersUpdate = value;
                Save();
            }
        }

        [SerializeField, Tooltip("Build Settings変更時にシーンenumを自動更新するか。")]
        private bool _autoSceneListUpdate = true;

        [SerializeField, Tooltip("タグ変更時にタグenumを自動更新するか。")]
        private bool _autoTagsUpdate = false;

        [SerializeField, Tooltip("レイヤー変更時にレイヤーenumを自動更新するか。")]
        private bool _autoLayersUpdate = false;

        /// <summary> 現在の設定値をProjectSettingsへ保存する。 </summary>
        private void Save() => Save(true);
    }
}
