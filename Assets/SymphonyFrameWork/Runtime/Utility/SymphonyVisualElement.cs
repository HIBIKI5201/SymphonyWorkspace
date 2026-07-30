using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SymphonyFrameWork.Utility
{
    /// <summary>
    ///     VisualElementをCSで制御するベースクラス
    /// </summary>
    [UxmlElement]
    public abstract partial class SymphonyVisualElement : VisualElement
    {
        /// <summary>
        ///     初期化のタイプ
        /// </summary>
        [Flags]
        public enum InitializeType
        {
            None = 0,
            Absolute = 1 << 0,
            FullRangth = 1 << 1,
            PickModeIgnore = 1 << 2,
            All = Absolute | FullRangth | PickModeIgnore
        }

        /// <summary>
        ///     ロードの方法
        /// </summary>
        public enum LoadType
        {
            Resources = 0,
            Addressable = 1,
            AssetDataBase = 2
        }

        /// <summary> UXMLのパスと読込方法を指定して非同期初期化を開始する。 </summary>
        /// <param name="path"> 読み込むUXMLアセットのパス。 </param>
        /// <param name="initializeType"> ルート要素へ適用する初期化設定。 </param>
        /// <param name="loadType"> UXMLアセットの読込方法。 </param>
        public SymphonyVisualElement(string path, InitializeType initializeType = InitializeType.All,
            LoadType loadType = LoadType.Addressable)
        {
            InitializeTask = Initialize(path, initializeType, loadType);
        }

        /// <summary>
        ///     初期化処理のタスク
        /// </summary>
        public Task InitializeTask { get; private set; }

        /// <summary>
        ///     初期化処理
        /// </summary>
        /// <param name="path">UXMLのパス</param>
        /// <param name="type">初期化のタイプ</param>
        /// <param name="loadType"> UXMLアセットの読込方法。 </param>
        /// <returns> UXML読込とサブクラス初期化を表すTask。 </returns>
        private async Task Initialize(string path, InitializeType type, LoadType loadType)
        {
            VisualTreeAsset treeAsset = default;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"{name} failed initialize");
                return;
            }

            switch (loadType)
            {
                case LoadType.Resources:
                    treeAsset = Resources.Load<VisualTreeAsset>(path);
                    break;
                    
                case LoadType.Addressable:
                    AsyncOperationHandle<VisualTreeAsset> asyncOperation 
                        = Addressables.LoadAssetAsync<VisualTreeAsset>(path);
                    await asyncOperation.Task;

                    if (asyncOperation.Status == AsyncOperationStatus.Succeeded)
                    {
                        treeAsset = asyncOperation.Result;
                    }
                    else
                    {
                        Debug.LogError($"Failed to load UXML file \nfrom : {path} \nusing Addressables");
                        return;
                    }
                    asyncOperation.Release();
                    break;

                case LoadType.AssetDataBase:
#if UNITY_EDITOR
                    treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
#else
                        Debug.Log("AssetDataBaseを使用したロードはエディタ専用です");
#endif
                    break;
            }


            if (treeAsset != null)
            {
                #region 親エレメントの初期化

                treeAsset.CloneTree(this);

                if ((type & InitializeType.PickModeIgnore) != 0)
                {
                    RegisterCallback<KeyDownEvent>(e => e.StopPropagation());
                    pickingMode = PickingMode.Ignore;
                }

                if ((type & InitializeType.Absolute) != 0) { style.position = Position.Absolute; }

                if ((type & InitializeType.FullRangth) != 0)
                {
                    style.height = Length.Percent(100);
                    style.width = Length.Percent(100);
                }

                #endregion

                // UI要素の取得
                await Initialize_S(this);
            }
            else
            {
                Debug.LogError($"Failed to load UXML file \nfrom : {path}");
            }
        }

        /// <summary>
        ///     サブクラス固有の初期化処理
        /// </summary>
        /// <param name="root">ロードしたUXMLのコンテナ</param>
        /// <returns> サブクラス固有の初期化処理を表すValueTask。 </returns>
        protected abstract ValueTask Initialize_S(VisualElement root);
    }
}
