using System;
using System.Threading;
using System.Threading.Tasks;

using SymphonyFrameWork.Config;
using SymphonyFrameWork.Exceptions;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary>
    ///     シーンのロードを管理するクラス
    /// </summary>
    public static class SceneLoader
    {
        private static SceneLoadManager _manager;
        private static SceneLoadData _data;

        /// <summary>
        ///     ロードされているシーンを返す。
        /// </summary>
        /// <param name="sceneName"> 取得するシーン名。 </param>
        /// <param name="scene"> 取得できたロード済みシーン。 </param>
        /// <returns> ロード済みシーンを取得できた場合はtrue。 </returns>
        public static bool GetExistScene(string sceneName, out Scene scene)
        {
            EnsureInitialized();
            scene = default;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            bool hasTrackedScene = _data.TryGetSceneInfo(sceneName, out SceneLoadData.SceneInfo info);
            if (hasTrackedScene && IsLoadedScene(info.Scene))
            {
                scene = info.Scene;
                return true;
            }

            Scene actualScene = SceneManager.GetSceneByName(sceneName);
            if (IsLoadedScene(actualScene))
            {
                _data.UpsertScene(sceneName, actualScene, hasTrackedScene ? info.Priority : 0);
                scene = actualScene;
                return true;
            }

            _data.RemoveScene(sceneName);
            return false;
        }

        /// <summary>
        ///     シーンが存在するかどうか。
        /// </summary>
        /// <param name="sceneName"> 存在を確認するシーン名。 </param>
        /// <returns> シーンが追跡中の場合はtrue。 </returns>
        public static bool IsExist(string sceneName)
        {
            EnsureInitialized();
            return _data.IsExistScene(sceneName);
        }

        /// <summary>
        ///     シーンの状態を返す。
        /// </summary>
        /// <param name="sceneName"> 状態を取得するシーン名。 </param>
        /// <param name="state"> 取得できたシーン状態。 </param>
        /// <returns> 状態を取得できた場合はtrue。 </returns>
        public static bool TryGetState(string sceneName, out SceneLoadState state)
        {
            EnsureInitialized();
            return _data.TryGetSceneState(sceneName, out state);
        }

        /// <summary>
        ///     シーンをアクティブにする。
        /// </summary>
        /// <param name="sceneName"> アクティブにするロード済みシーン名。 </param>
        /// <returns> アクティブシーンを変更できた場合はtrue。 </returns>
        public static bool SetActiveScene(string sceneName)
        {
            EnsureInitialized();
            return _manager.TrySetActiveScene(sceneName);
        }

        /// <summary>
        ///     既にロード済みのシーンを指定優先度で追跡登録する。
        /// </summary>
        /// <param name="sceneName"> シーン名。 </param>
        /// <param name="priority"> 優先度。 </param>
        /// <returns> 登録に成功した場合はtrue。 </returns>
        public static bool RegisterLoadedScene(string sceneName, int priority)
        {
            EnsureInitialized();
            return _manager.TryRegisterLoadedScene(sceneName, priority);
        }

        /// <summary>
        ///     シーンをロードする。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <param name="loadingAction">ロードの進捗率を引数にしたメソッド</param>
        /// <param name="mode"> AdditiveまたはSingle相当のロード方式。 </param>
        /// <param name="priority"> ロード後のアクティブシーン選択に使用する優先度。 </param>
        /// <param name="token"> ロード処理を中断するためのトークン。 </param>
        /// <returns>ロードに成功したか</returns>
        /// <exception cref="ArgumentException"> シーン名がnull、空、空白の場合。 </exception>
        /// <exception cref="SceneInitializationException"> ロード後の依存注入または非同期初期化に失敗した場合。 </exception>
        public static ValueTask<bool> LoadScene(
            string sceneName,
            Action<float> loadingAction = null,
            LoadSceneMode mode = LoadSceneMode.Additive,
            int priority = 0,
            CancellationToken token = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("シーン名を指定してください。", nameof(sceneName));
            }

            return _manager.LoadScene(
                name: sceneName,
                loadingAction: loadingAction,
                mode: mode,
                priority: priority,
                token: token);
        }

        /// <summary>
        ///     シーンをロードする。
        /// </summary>
        /// <param name="sceneNames"> ロードするシーン名の一覧。 </param>
        /// <param name="loadingAction"> 全シーンの平均進捗率を受け取る処理。 </param>
        /// <param name="token"> ロード処理を中断するためのトークン。 </param>
        /// <returns> すべてのシーンをロードできた場合はtrue。 </returns>
        /// <exception cref="ArgumentNullException"> シーン名一覧がnullの場合。 </exception>
        /// <exception cref="ArgumentException"> シーン名一覧が空、または無効なシーン名を含む場合。 </exception>
        /// <exception cref="SceneInitializationException"> ロード後の依存注入または非同期初期化に失敗した場合。 </exception>
        public static ValueTask<bool> LoadScenes(
            string[] sceneNames,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            EnsureInitialized();
            ValidateSceneNames(sceneNames);

            return _manager.LoadScenes(
                sceneNames,
                loadingAction,
                token);
        }

        /// <summary>
        ///     シーンをアンロードする。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <param name="loadingAction">ロードの進捗率を引数にしたメソッド</param>
        /// <param name="token"> アンロード処理を中断するためのトークン。 </param>
        /// <returns>アンロードに成功したか</returns>
        /// <exception cref="ArgumentException"> シーン名がnull、空、空白の場合。 </exception>
        public static ValueTask<bool> UnloadScene(
            string sceneName,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("シーン名を指定してください。", nameof(sceneName));
            }

            return _manager.UnloadScene(
                sceneName,
                loadingAction,
                token
                );
        }

        /// <summary>
        ///     シーンをアンロードする。
        /// </summary>
        /// <param name="sceneNames"> アンロードするシーン名の一覧。 </param>
        /// <param name="loadingAction"> 全シーンの平均進捗率を受け取る処理。 </param>
        /// <param name="token"> アンロード処理を中断するためのトークン。 </param>
        /// <returns> すべてのシーンをアンロードできた場合はtrue。 </returns>
        /// <exception cref="ArgumentNullException"> シーン名一覧がnullの場合。 </exception>
        /// <exception cref="ArgumentException"> シーン名一覧が空、または無効なシーン名を含む場合。 </exception>
        public static ValueTask<bool> UnloadScenes(
            string[] sceneNames,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            EnsureInitialized();
            ValidateSceneNames(sceneNames);

            return _manager.UnloadScenes(
                sceneNames,
                loadingAction,
                token);
        }

        /// <summary>
        ///     シーンがロードされた時に実行されるイベントを登録する。
        ///     ロード済みの場合は即座に実行される。
        /// </summary>
        /// <param name="sceneName"> ロード完了を監視するシーン名。 </param>
        /// <param name="action"> ロード完了後に一度実行する処理。 </param>
        public static void RegisterAfterSceneLoad(string sceneName, Action action)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("シーン名を指定してください。", nameof(sceneName));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _data.AddLoadedAction(sceneName, action);
        }

        /// <summary>
        ///     指定したシーンがロードされるまで待機する
        /// </summary>
        /// <param name="sceneName"> ロード完了を待機するシーン名。 </param>
        /// <param name="token"> 待機を中断するためのトークン。 </param>
        public static async ValueTask WaitForLoadSceneAsync(string sceneName, CancellationToken token = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("シーン名を指定してください。", nameof(sceneName));
            }

            while (!_data.TryGetSceneState(sceneName, out SceneLoadState state) || state < SceneLoadState.Complete)
            {
                await Awaitable.NextFrameAsync(token);
            }
        }

        /// <summary>
        ///     Orchestratorからの初期化。
        /// </summary>
        /// <param name="destroyCancellationToken"> システム破棄時に状態を消去するためのトークン。 </param>
        internal static void Initialize(CancellationToken destroyCancellationToken)
        {
            _destroyRegistration.Dispose();
            ResetRuntimeState();
            _data = new SceneLoadData();
            _manager = new(_data);
            _destroyRegistration = destroyCancellationToken.Register(ResetRuntimeState);
        }

        /// <summary> 追跡データと管理インスタンスを破棄して未初期化状態へ戻す。 </summary>
        private static void ResetRuntimeState()
        {
            _data?.Clear();
            _manager = null;
            _data = null;
        }

        /// <summary> Scene Loaderが利用可能な状態か検証する。 </summary>
        private static void EnsureInitialized()
        {
            if (_manager == null || _data == null)
            {
                throw new SymphonyNotInitializedException(typeof(SceneLoader));
            }
        }

        /// <summary> 複数シーン操作へ渡されたシーン名一覧を検証する。 </summary>
        /// <param name="sceneNames"> 検証するシーン名一覧。 </param>
        private static void ValidateSceneNames(string[] sceneNames)
        {
            if (sceneNames == null)
            {
                throw new ArgumentNullException(nameof(sceneNames));
            }

            if (sceneNames.Length == 0)
            {
                throw new ArgumentException("シーン名を1件以上指定してください。", nameof(sceneNames));
            }

            for (int i = 0; i < sceneNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sceneNames[i]))
                {
                    throw new ArgumentException(
                        $"インデックス{i}のシーン名がnullまたは空です。",
                        nameof(sceneNames));
                }
            }
        }

        /// <summary>
        ///     ゲーム開始時の初期化処理
        /// </summary>
        /// <param name="config"> 起動時のシーン整理とロード設定。 </param>
        internal static async ValueTask AfterSceneLoad(SceneManagerConfig config)
        {
            await InitializeSceneLoad(config);
        }

        /// <summary> Unityシーンが有効かつロード済みで、名前を持つか確認する。 </summary>
        private static bool IsLoadedScene(Scene scene) =>
            scene.IsValid()
            && scene.isLoaded
            && !string.IsNullOrWhiteSpace(scene.name);

        /// <summary>
        ///     シーンの初期化
        /// </summary>
        /// <param name="config"> 起動時のシーン整理とロード設定。 </param>
        /// <returns> シーン初期化処理を表すValueTask。 </returns>
        private static async ValueTask InitializeSceneLoad(SceneManagerConfig config)
        {
            // 現状のシーン状況を保存する。
            _manager.ResetSceneData();

            // シーンリセットの条件が揃っていない場合は何もしない。
            if (config == null
                || !config.IsResetAndLoadOnPlay 
                || config.InitializeSceneList == null
                || config.InitializeSceneList.Length <= 0) { return; }


            // シーンのロード状況をリセットする。
            string[] resetIgnoreScenes = GetResetIgnoreScenes(config);
            await SceneResetter.ResetScene(_manager, resetIgnoreScenes);

            // ロードしていない初期シーンをロードする。
            await SceneResetter.LoadScene(_manager, config);
        }

        /// <summary> Configのリセット対象外シーンと初期シーンを統合する。 </summary>
        private static string[] GetResetIgnoreScenes(SceneManagerConfig config)
        {
            int resetIgnoreCount = config.ResetIgnoreSceneList?.Length ?? 0;
            int initializeSceneCount = config.InitializeSceneList?.Length ?? 0;

            string[] resetIgnoreScenes = new string[resetIgnoreCount + initializeSceneCount];

            if (0 < resetIgnoreCount)
            {
                Array.Copy(config.ResetIgnoreSceneList, resetIgnoreScenes, resetIgnoreCount);
            }

            for (int i = 0; i < initializeSceneCount; i++)
            {
                resetIgnoreScenes[resetIgnoreCount + i] = config.InitializeSceneList[i];
            }

            return resetIgnoreScenes;
        }

        private static CancellationTokenRegistration _destroyRegistration;
    }
}
