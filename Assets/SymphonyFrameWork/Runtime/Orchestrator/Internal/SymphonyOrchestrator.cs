using System;

using SymphonyFrameWork.Config;
using SymphonyFrameWork.Debugger.HUD;
using SymphonyFrameWork.System;
using SymphonyFrameWork.System.SaveSystem;
using SymphonyFrameWork.System.SceneLoad;
using SymphonyFrameWork.System.ServiceLocate;

using UnityEngine;

namespace SymphonyFrameWork.Orchestrator
{
    /// <summary>
    ///     SymphonyFrameWorkの永続オブジェクトを管理する、パッケージ全体のComposition Rootです。
    /// </summary>
    internal static class SymphonyOrchestrator
    {
        /// <summary>
        ///     ルートGameObjectをシーン遷移時も破棄されない永続オブジェクトにする。
        /// </summary>
        /// <param name="gameObject"> 永続化するルートGameObject。 </param>
        internal static void PreserveObject(GameObject gameObject)
        {
            if (!gameObject)
            {
                return;
            }

            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        /// <summary> シーン遷移時も破棄されないコンポーネントを生成する。 </summary>
        /// <typeparam name="T"> 生成するコンポーネントの型。 </typeparam>
        /// <returns> 生成したコンポーネント。 </returns>
        internal static T CreateSystemObject<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            T component = go.AddComponent<T>();
            PreserveObject(go);
            return component;
        }

        private static SymphonyOrchestratorObject _systemObject;

        /// <summary>
        ///     永続オブジェクトを生成し、各サブシステムを初期化する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GameBeforeSceneLoaded()
        {
            var systemGameObject = new GameObject(nameof(SymphonyOrchestrator));
            _systemObject = systemGameObject.AddComponent<SymphonyOrchestratorObject>();
            PreserveObject(systemGameObject);
            SaveSystem.SaveSystem.Initialize(
                _systemObject.destroyCancellationToken,
                ResolveSaveDataLoader);

            //各クラスの初期化
            PauseManager.Initialize();
            ServiceLocator.Initialize(_systemObject.destroyCancellationToken);
            SceneLoader.Initialize(_systemObject.destroyCancellationToken);
            AudioManager.Initialize(
                SymphonyConfigLocator.GetConfig<AudioManagerConfig>());

            SymphonyDebugHUD.Initialize();

            GC.Collect();
        }

        /// <summary> 初期シーンのロード後にシーン管理を開始する。 </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void GameAfterSceneLoaded()
        {
            SceneManagerConfig config =
                SymphonyConfigLocator.GetConfig<SceneManagerConfig>();
            _ = SceneLoader.AfterSceneLoad(config);
        }

        /// <summary>
        ///     現在のConfigからセーブデータローダーを解決する。
        /// </summary>
        /// <returns> Configで選択されたローダー。未設定の場合は既定のローダー。 </returns>
        private static SaveDataLoader ResolveSaveDataLoader()
        {
            SaveSystemConfig config =
                SymphonyConfigLocator.GetConfig<SaveSystemConfig>();
            return config?.Loader ?? new JsonUtilitySaveDataLoader();
        }
    }
}
