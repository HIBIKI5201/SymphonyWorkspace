using SymphonyFrameWork.Core;
using SymphonyFrameWork.System.SceneLoad;
using SymphonyFrameWork.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary> SceneLoaderが追跡するシーン状態を一覧表示する管理パネル。 </summary>
    [UxmlElement]
    public sealed partial class SceneLoaderWindow : SymphonyVisualElement
    {
        private Dictionary<string, SceneLoadData.SceneInfo> _sceneDict;
        private FieldInfo _dataField;
        private ListView _sceneList;

        /// <summary> 管理パネル用UXMLの非同期初期化を開始する。 </summary>
        public SceneLoaderWindow() : base(
            SymphonyAdministrator.UITK_UXML_PATH + "SceneLoaderWindow.uxml",
            InitializeType.None,
            LoadType.AssetDataBase)
        { }

        /// <summary> SceneLoaderの追跡データを参照するシーン一覧を構成する。 </summary>
        protected override ValueTask Initialize_S(VisualElement container)
        {
            _dataField = typeof(SceneLoader).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);

            UpdateSceneDict();

            _sceneList = container.Q<ListView>("scene-list");

            _sceneList.makeItem = () => new Label();

            _sceneList.bindItem = (element, index) =>
            {
                var kvp = GetSceneList()[index];
                (element as Label).text = $"Name : {kvp.Key}\nState : {kvp.Value.State}, Priority : {kvp.Value.Priority}";
            };

            _sceneList.itemsSource = GetSceneList();
            _sceneList.selectionType = SelectionType.None;

            return default;
        }

        /// <summary> SceneLoader内部の最新シーン辞書参照を取得する。 </summary>
        private void UpdateSceneDict()
        {
            if (_dataField == null) return;

            var sceneLoadDataInstance = _dataField.GetValue(null);

            if (sceneLoadDataInstance == null)
            {
                _sceneDict = new Dictionary<string, SceneLoadData.SceneInfo>();
                return;
            }

            var sceneDictField =
                sceneLoadDataInstance.GetType()
                .GetField("_sceneDict",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            if (sceneDictField != null)
            {
                _sceneDict = (Dictionary<string, SceneLoadData.SceneInfo>)sceneDictField.GetValue(sceneLoadDataInstance);
            }
            else
            {
                _sceneDict = new Dictionary<string, SceneLoadData.SceneInfo>();
            }
        }

        /// <summary> 表示時の列挙変更を避けるため、シーン辞書のスナップショットを生成する。 </summary>
        private List<KeyValuePair<string, SceneLoadData.SceneInfo>> GetSceneList()
        {
            UpdateSceneDict();
            return _sceneDict != null
                ? new List<KeyValuePair<string, SceneLoadData.SceneInfo>>(_sceneDict)
                : new List<KeyValuePair<string, SceneLoadData.SceneInfo>>();
        }

        /// <summary> シーン一覧を最新の追跡状態で再構築する。 </summary>
        public void Update()
        {
            if (_sceneList != null)
            {
                _sceneList.itemsSource = GetSceneList();
                _sceneList.Rebuild();
            }
        }
    }
}
