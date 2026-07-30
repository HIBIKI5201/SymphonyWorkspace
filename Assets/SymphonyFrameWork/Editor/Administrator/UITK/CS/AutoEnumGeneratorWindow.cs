using SymphonyFrameWork.Utility;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary> enum自動更新設定と手動生成操作を提供する管理パネル。 </summary>
    [UxmlElement]
    public sealed partial class AutoEnumGeneratorWindow : SymphonyVisualElement
    {
        /// <summary> 管理パネル用UXMLの非同期初期化を開始する。 </summary>
        public AutoEnumGeneratorWindow() : base(
            SymphonyAdministrator.UITK_UXML_PATH + "AutoEnumGeneratorWindow.uxml",
            InitializeType.None,
            LoadType.AssetDataBase)
        { }
        /// <summary> 自動生成設定のToggleと手動生成Buttonを構成する。 </summary>
        protected override ValueTask Initialize_S(VisualElement container)
        {
            //コンフィグデータを取得
            var config = SymphonyEditorConfigLocator.GetConfig<AutoEnumGeneratorConfig>();

            var sceneList = GetElement("scene");
            sceneList.toggle.value = config.AutoSceneListUpdate;
            sceneList.toggle.RegisterValueChangedCallback(
                evt => config.AutoSceneListUpdate = evt.newValue);
            sceneList.button.clicked += () => AutoEnumGenerator.SceneListEnumGenerate();

            var tags = GetElement("tags");
            tags.toggle.value = config.AutoTagsUpdate;
            tags.toggle.RegisterValueChangedCallback(
                evt => config.AutoTagsUpdate = evt.newValue);
            tags.button.clicked += () => AutoEnumGenerator.TagsEnumGenerate();

            var layers = GetElement("layers");
            layers.toggle.value = config.AutoLayerUpdate;
            layers.toggle.RegisterValueChangedCallback(
                evt => config.AutoLayerUpdate = evt.newValue);
            layers.button.clicked += () => AutoEnumGenerator.LayersEnumGenerate();

            return default;

            (Toggle toggle, Button button) GetElement(string name) =>
                container.Q<VisualElement>(name) switch
                    { VisualElement ve => (ve.Q<Toggle>(), ve.Q<Button>()) };
        }
    }
}
