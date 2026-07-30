using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using SymphonyFrameWork.Core;
using SymphonyFrameWork.System.ServiceLocate;
using SymphonyFrameWork.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary> Service Locatorの登録状態とデバッグログ設定を表示する管理パネル。 </summary>
    [UxmlElement]
    public sealed partial class ServiceLocatorWindow : SymphonyVisualElement
    {
        private Dictionary<Type, object> _locateDict;
        private FieldInfo _lazyDataField;
        private ListView _locateList;

        /// <summary> 管理パネル用UXMLの非同期初期化を開始する。 </summary>
        public ServiceLocatorWindow() : base(
            SymphonyAdministrator.UITK_UXML_PATH + "ServiceLocatorWindow.uxml",
            InitializeType.None,
            LoadType.AssetDataBase)
        { }

        /// <summary> 登録一覧とService Locatorのログ設定Toggleを構成する。 </summary>
        protected override ValueTask Initialize_S(VisualElement container)
        {
            _lazyDataField = typeof(ServiceLocator).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);

            UpdateLocateDict();

            _locateList = container.Q<ListView>("locate-list");

            _locateList.makeItem = () => new Label();

            // 項目のバインド（データを UI に反映）
            _locateList.bindItem = (element, index) =>
            {
                var kvp = GetLocateList()[index];
                if (kvp.Value is UnityEngine.Object unityObject && unityObject == null)
                {
                    (element as Label).text = $"type : {kvp.Key.Name}\nobj : (Destroyed)";
                    return;
                }
                // Componentの場合のみnameプロパティにアクセス
                string objName = (kvp.Value is Component component) ? component.name : kvp.Value.GetType().Name;
                (element as Label).text = $"type : {kvp.Key.Name}\nobj : {objName}";
            };
            
            // データのセット
            _locateList.itemsSource = GetLocateList();

            // 選択タイプの設定
            _locateList.selectionType = SelectionType.None;

            //ログのコンフィグを初期化
            var setInstanceLogActive = container.Q<Toggle>("set_instance-log-active");
            InitializeToggle(setInstanceLogActive,
                EditorSymphonyConstant.ServiceLocatorSetInstanceKey,
                EditorSymphonyConstant.ServiceLocatorSetInstanceDefault);

            var getInstanceLogActive = container.Q<Toggle>("get_instance-log-active");
            InitializeToggle(getInstanceLogActive,
                EditorSymphonyConstant.ServiceLocatorGetInstanceKey,
                EditorSymphonyConstant.ServiceLocatorGetInstanceDefault);

            var destroyInstanceLogActive = container.Q<Toggle>("destroy_instance-log-active");
            InitializeToggle(destroyInstanceLogActive,
                EditorSymphonyConstant.ServiceLocatorDestroyInstanceKey,
                EditorSymphonyConstant.ServiceLocatorDestroyInstanceDefault);

            return default;
        }

        /// <summary> ServiceLocator内部の最新登録辞書参照を取得する。 </summary>
        private void UpdateLocateDict()
        {
            if (_lazyDataField == null)
                return;

            // static フィールドなので null を渡す
            var serviceLocatorDataInstance = _lazyDataField.GetValue(null);

            if (serviceLocatorDataInstance == null)
            {
                _locateDict = new Dictionary<Type, object>();
                return;
            }

            var singletonObjectsField =
                serviceLocatorDataInstance.GetType()
                .GetField("_locateObjects",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            if (singletonObjectsField != null)
            {
                _locateDict =
                    (Dictionary<Type, object>)
                    singletonObjectsField.GetValue(serviceLocatorDataInstance);
            }
            else
            {
                _locateDict = new Dictionary<Type, object>();
            }
        }

        /// <summary> 表示時の列挙変更を避けるため、登録辞書のスナップショットを生成する。 </summary>
        private List<KeyValuePair<Type, object>> GetLocateList()
        {
            UpdateLocateDict();
            return _locateDict != null
                ? new List<KeyValuePair<Type, object>>(_locateDict)
                : new List<KeyValuePair<Type, object>>();
        }

        /// <summary> 登録一覧を最新のService Locator状態で再構築する。 </summary>
        public void Update()
        {
            if (_locateList != null)
            {
                _locateList.itemsSource = GetLocateList();
                _locateList.Rebuild();
            }
        }

        /// <summary> EditorPrefsに保存されるログ設定Toggleを初期化する。 </summary>
        private void InitializeToggle(Toggle toggle, string key, bool defaultValue)
        {
            if (toggle != null)
            {
                toggle.value = EditorPrefs.GetBool(key, defaultValue);
                toggle.RegisterValueChangedCallback(e => EditorPrefs.SetBool(key, e.newValue));
            }
        }
    }
}
