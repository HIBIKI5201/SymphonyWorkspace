using SymphonyFrameWork.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     SymphonyFrameWorkの管理パネルを表示するクラス
    /// </summary>
    public sealed class SymphonyAdministrator : EditorWindow
    {
        private const string WINDOW_NAME = "Symphony Administrator";

        /// <summary> 管理パネルを構成するUXMLファイルの基準パス。 </summary>
        public static string UITK_UXML_PATH = EditorSymphonyConstant.UITK_PATH + "UXML/";

        private PauseWindow _pauseWindow;
        private ServiceLocatorWindow _serviceLocatorWindow;
        private SceneLoaderWindow _sceneLoaderWindow;
        private AutoEnumGeneratorWindow _generatorWindow;
        private SaveDataRegistryWindow _saveDataRegistryWindow;

        /// <summary> 各管理パネルの表示内容をEditor更新ごとに同期する。 </summary>
        private void Update()
        {
            _pauseWindow?.Update();
            _serviceLocatorWindow?.Update();
            _sceneLoaderWindow?.Update();
            _saveDataRegistryWindow?.Update();
        }

        /// <summary> UXMLから管理パネルを構築し、Editor更新処理を購読する。 </summary>
        private void OnEnable()
        {
            var container = LoadWindow();

            if (container != null)
            {
                _pauseWindow = container.Q<PauseWindow>();
                _serviceLocatorWindow = container.Q<ServiceLocatorWindow>();
                _sceneLoaderWindow = container.Q<SceneLoaderWindow>();
                _generatorWindow = container.Q<AutoEnumGeneratorWindow>();
                _saveDataRegistryWindow = container.Q<SaveDataRegistryWindow>();
            }
            else
            {
                Debug.LogWarning("ウィンドウがロードできませんでした");
            }

            EditorApplication.update += Update;
        }

        /// <summary> Editor更新処理と保持中の管理パネルリソースを解除する。 </summary>
        private void OnDisable()
        {
            EditorApplication.update -= Update;
            _saveDataRegistryWindow?.Dispose();
            _saveDataRegistryWindow = null;
        }


        /// <summary>
        ///     ウィンドウ表示
        /// </summary>
        [MenuItem(SymphonyConstant.WINDOW_MENU_PATH + WINDOW_NAME, priority = 0)]
        public static void ShowWindow()
        {
            var wnd = GetWindow<SymphonyAdministrator>();
            wnd.titleContent = new GUIContent(WINDOW_NAME);
        }

        /// <summary>
        ///     UXMLを追加
        /// </summary>
        /// <returns> インスタンス化した管理ウィンドウのルート要素。 </returns>
        private TemplateContainer LoadWindow()
        {
            rootVisualElement.Clear();

            var windowTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorSymphonyConstant.UITK_PATH + "SymphonyWindow.uxml");
            ;
            if (windowTree != null)
            {
                var windowElement = windowTree.Instantiate();
                rootVisualElement.Add(windowElement);
                return windowElement;
            }

            Debug.LogError("ウィンドウが見つかりません");
            return null;
        }
    }
}
