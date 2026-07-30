using SymphonyFrameWork.System;
using SymphonyFrameWork.Utility;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary> PauseManagerの状態表示と操作を提供する管理パネル。 </summary>
    [UxmlElement]
    public sealed partial class PauseWindow : SymphonyVisualElement
    {
        private FieldInfo _pauseInfo;
        private Label _pauseText;
        private VisualElement _pauseVisual;

        /// <summary> 管理パネル用UXMLの非同期初期化を開始する。 </summary>
        public PauseWindow() : base(
            SymphonyAdministrator.UITK_UXML_PATH + "PauseWindow.uxml",
            InitializeType.None,
            LoadType.AssetDataBase)
        { }

        /// <summary> PauseManagerへの操作ボタンと状態表示要素を構成する。 </summary>
        protected override ValueTask Initialize_S(VisualElement container)
        {
            // _pause フィールドを取得
            _pauseInfo = typeof(PauseManager).GetField("_pause", BindingFlags.Static | BindingFlags.NonPublic);

            _pauseVisual = container.Q<VisualElement>("pause");
            _pauseText = container.Q<Label>("pause-text");

            container.Q<Button>("button-pause").clicked += () => PauseManager.Pause = true;
            container.Q<Button>("button-resume").clicked += () => PauseManager.Pause = false;

            return default;
        }

        /// <summary> 現在のポーズ状態を表示色と文字列へ反映する。 </summary>
        public void Update()
        {
            if (_pauseVisual != null && _pauseInfo != null)
            {
                var active = (bool)_pauseInfo.GetValue(null);
                _pauseVisual.style.backgroundColor = new StyleColor(active ? Color.green : Color.red);
                _pauseText.text = active ? "True" : "False";
            }
        }
    }
}
