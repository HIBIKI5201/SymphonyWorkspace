using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SymphonyFrameWork.Debugger.Logger;
using SymphonyFrameWork.System.ServiceLocate;
using SymphonyFrameWork.Utility;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary> Unity SceneManagerの操作とシーン追跡状態の同期を担当する。 </summary>
    internal sealed class SceneLoadManager
    {
        /// <summary> 状態の保存先を指定してシーン管理処理を生成する。 </summary>
        public SceneLoadManager(SceneLoadData data)
        {
            _data = data;
        }

        /// <summary> ロード済みの指定シーンをアクティブに設定する。 </summary>
        public bool TrySetActiveScene(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                SymphonyDebugLogger.LogDirect("Scene name is null or empty.");
                return false;
            }

            bool hasTrackedScene = _data.TryGetSceneInfo(name, out SceneLoadData.SceneInfo info);
            if (!hasTrackedScene || !IsLoadedScene(info.Scene))
            {
                Scene actualScene = SceneManager.GetSceneByName(name);
                if (!IsLoadedScene(actualScene))
                {
                    _data.RemoveScene(name);
                    SymphonyDebugLogger.LogDirect($"{name} is not loaded scene");
                    return false;
                }

                int priority = hasTrackedScene ? info.Priority : 0;
                _data.UpsertScene(name, actualScene, priority);
                info = new SceneLoadData.SceneInfo(actualScene, priority, SceneLoadState.Complete);
            }

            if (!SceneManager.SetActiveScene(info.Scene))
            {
                SymphonyDebugLogger.LogDirect($"Failed set active scene : {name}");
                return false;
            }

            _data.SetActiveScene(name, info.Priority);
            return true;
        }

        /// <summary>
        ///     シーンをロードする。
        /// </summary>
        /// <param name="name">シーン名</param>
        /// <param name="loadingAction">ロードの進捗率を引数にしたメソッド</param>
        /// <param name="mode"> AdditiveまたはSingle相当のロード方式。 </param>
        /// <param name="priority"> ロード後のアクティブシーン選択に使用する優先度。 </param>
        /// <param name="token"> ロード処理を中断するためのトークン。 </param>
        /// <returns>ロードに成功したか</returns>
        public async ValueTask<bool> LoadScene(
            string name,
            Action<float> loadingAction = null,
            LoadSceneMode mode = LoadSceneMode.Additive,
            int priority = 0,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogError("scene name is null or empty.");
                return false;
            }

            bool hasTrackedScene = _data.TryGetSceneInfo(name, out SceneLoadData.SceneInfo trackedInfo);
            if (hasTrackedScene && IsLoadedScene(trackedInfo.Scene))
            {
                if (_data.ActiveScene.Priority <= trackedInfo.Priority)
                {
                    TrySetActiveScene(name);
                }

                _data.InvokeLoadedAction(name);
                return true;
            }

            Scene actualLoadedScene = SceneManager.GetSceneByName(name);
            if (IsLoadedScene(actualLoadedScene))
            {
                int trackedPriority = hasTrackedScene ? trackedInfo.Priority : priority;
                _data.UpsertScene(name, actualLoadedScene, trackedPriority);

                if (_data.ActiveScene.Priority <= trackedPriority)
                {
                    TrySetActiveScene(name);
                }

                _data.InvokeLoadedAction(name);
                return true;
            }

            if (hasTrackedScene)
            {
                _data.RemoveScene(name);
            }

            #region ロード開始
            var operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"{name} is not register. check scene list of build setting.");
                return false;
            }

            _data.LoadStart(name, priority);

            #endregion

            #region ロード中。
            await SymphonyTask.WaitUntil(
                () =>
                {
                    loadingAction?.Invoke(operation.progress);
                    return operation.isDone;
                },
                token);


            // シングルロードの場合、対象以外のシーンをアンロード。
            if (mode == LoadSceneMode.Single)
            {
                await ResetScene(name);
            }
            #endregion

            #region ロード完了後。
            Scene loadedScene = SceneManager.GetSceneByName(name);

            //辞書にシーン名とシーン情報を保存。
            var isLoadSuccess = loadedScene.IsValid() && loadedScene.isLoaded;
            if (!isLoadSuccess)
            {
                Debug.LogWarning($"Failed Loading Scene: {name}");
                _data.LoadFail(name);
                return false;
            }

            //シングルロードの場合は辞書をクリアする。
            if (mode == LoadSceneMode.Single)
            {
                ResetSceneData();
            }

            _data.LoadComplete(name, loadedScene);

            if (_data.ActiveScene.Priority <= priority)
            {
                TrySetActiveScene(name);
            }

            //ロード終了後にロード待ちしていたイベントを実行。
            _data.InvokeLoadedAction(name);
            #endregion

            #region シーン上のオブジェクトの初期化を実行する。
            GameObject[] objs = loadedScene.GetRootGameObjects();

            List<Task> initializeTasks = new();

            foreach (GameObject obj in objs)
            {
                obj.TryGetComponent(out IInjectable injectable);
                obj.TryGetComponent(out IInitializeAsync initializer);

                if (injectable != null || initializer != null)
                {
                    initializeTasks.Add(
                        InitializeRootObjectAsync(
                            name,
                            obj,
                            injectable,
                            initializer));
                }
            }

            if (0 < initializeTasks.Count) //初期化が終了するまで待機。
            {
                await Task.WhenAll(initializeTasks);
            }
            #endregion

            return isLoadSuccess;
        }

        /// <summary>
        ///     シーンをロードする。
        /// </summary>
        /// <param name="names"> ロードするシーン名の一覧。 </param>
        /// <param name="loadingAction"> 全シーンの平均進捗率を受け取る処理。 </param>
        /// <param name="token"> ロード処理を中断するためのトークン。 </param>
        /// <returns> すべてのシーンをロードできた場合はtrue。 </returns>
        public async ValueTask<bool> LoadScenes(
            string[] names,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            foreach (string scene in names)
            {
                if (string.IsNullOrEmpty(scene))
                {
                    Debug.LogWarning($"load scenes is canceled because contain null or empty in scene names");
                    return false;
                }
            }

            ValueTask<bool>[] loadTasks = new ValueTask<bool>[names.Length]; // シーンごとのロードタスク。
            float[] progresses = new float[names.Length]; // シーンごとの進捗率。

            // 全てのシーンのロードを開始。
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                loadTasks[i] = LoadScene(names[i], n => progresses[index] = n, token: token);
            }

            StringBuilder debugProgress = new StringBuilder();
            // ロード中の進捗率を計算して通知。
            while (!token.IsCancellationRequested)
            {
                // 全てのシーンの平均進捗率を計算。
                float totalProgress = 0f;
                for (int i = 0; i < progresses.Length; i++)
                {
                    totalProgress += progresses[i];
                }
                float averageProgress = totalProgress / progresses.Length;
                loadingAction?.Invoke(averageProgress);

                #region デバッグ用に各シーンの進捗率をログ出力。
                debugProgress.Clear();
                debugProgress.AppendLine($"AverageProgress : {averageProgress}");
                for (int i = 0; i < progresses.Length; i++)
                {
                    debugProgress.Append($"\n  Scene {names[i]} Progress : {progresses[i]}");
                }
                Debug.Log(debugProgress.ToString());
                #endregion

                // 全てのシーンのロードが完了したか確認。
                bool allDone = true;
                for (int i = 0; i < loadTasks.Length; i++)
                {
                    if (!loadTasks[i].IsCompleted)
                    {
                        allDone = false;
                        break;
                    }
                }

                // 全てのシーンのロードが完了した場合、ループを抜ける。
                if (allDone) { break; }
                await Awaitable.NextFrameAsync(token);
            }

            if (token.IsCancellationRequested)
            {
                return false;
            }

            for (int i = 0; i < loadTasks.Length; i++)
            {
                if (!loadTasks[i].Result) { return false; }
            }

            return true;
        }

        /// <summary>
        ///     シーンをアンロードする。
        /// </summary>
        /// <param name="name">シーン名</param>
        /// <param name="loadingAction">ロードの進捗率を引数にしたメソッド</param>
        /// <param name="token"> アンロード処理を中断するためのトークン。 </param>
        /// <returns>アンロードに成功したか</returns>
        public async ValueTask<bool> UnloadScene(
            string name,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            bool hasTrackedScene = _data.TryGetSceneInfo(name, out SceneLoadData.SceneInfo trackedInfo);
            Scene targetScene = hasTrackedScene && IsLoadedScene(trackedInfo.Scene)
                ? trackedInfo.Scene
                : SceneManager.GetSceneByName(name);

            if (!IsLoadedScene(targetScene))
            {
                _data.RemoveScene(name);
                Debug.LogWarning($"{name} is not loaded");
                return true;
            }

            if (!hasTrackedScene)
            {
                _data.UpsertScene(name, targetScene);
            }

            //アンロード開始。
            var operation = SceneManager.UnloadSceneAsync(name);
            if (operation == null)
            {
                Debug.LogError($"{name} is not register. check scene list of build setting.");
                return false;
            }

            _data.UnloadStart(name);

            //ロード中。
            await SymphonyTask.WaitUntil(
                () =>
                {
                    loadingAction?.Invoke(operation.progress);
                    return operation.isDone;
                },
                token);

            // アンロード完了後。
            _data.UnloadComplete(name);

            // アンロードしたシーンがアクティブシーンだった場合、アクティブシーンを変更する。
            if (name == _data.ActiveScene.Name)
            {
                if (_data.TryGetHighestPriorityLoadedSceneInfo(out SceneLoadData.SceneInfo info))
                {
                    TrySetActiveScene(info.Scene.name);
                }
            }

            return true;
        }

        /// <summary>
        ///     既にロード済みのシーンを指定優先度で追跡登録する。
        /// </summary>
        /// <param name="name"> シーン名。 </param>
        /// <param name="priority"> 優先度。 </param>
        /// <returns> 登録に成功した場合はtrue。 </returns>
        public bool TryRegisterLoadedScene(string name, int priority)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            bool hasTrackedScene = _data.TryGetSceneInfo(name, out SceneLoadData.SceneInfo trackedInfo);
            Scene scene = hasTrackedScene && IsLoadedScene(trackedInfo.Scene)
                ? trackedInfo.Scene
                : SceneManager.GetSceneByName(name);

            if (!IsLoadedScene(scene))
            {
                _data.RemoveScene(name);
                return false;
            }

            SceneLoadState state = hasTrackedScene ? trackedInfo.State : SceneLoadState.Complete;
            _data.UpsertScene(name, scene, priority, state);

            if (SceneManager.GetActiveScene().name == name)
            {
                _data.SetActiveScene(name, priority);
            }

            return true;
        }

        /// <summary>
        ///     シーンをアンロードする。
        /// </summary>
        /// <param name="names"> アンロードするシーン名の一覧。 </param>
        /// <param name="loadingAction"> 全シーンの平均進捗率を受け取る処理。 </param>
        /// <param name="token"> アンロード処理を中断するためのトークン。 </param>
        /// <returns> すべてのシーンをアンロードできた場合はtrue。 </returns>
        public async ValueTask<bool> UnloadScenes(
            string[] names,
            Action<float> loadingAction = null,
            CancellationToken token = default)
        {
            foreach (string scene in names)
            {
                if (string.IsNullOrEmpty(scene))
                {
                    Debug.LogWarning($"load scenes is canceled because contain null or empty in scene names");
                    return false;
                }
            }

            ValueTask<bool>[] loadTasks = new ValueTask<bool>[names.Length]; // シーンごとのロードタスク。
            float[] progresses = new float[names.Length]; // シーンごとの進捗率。

            // 全てのシーンのロードを開始。
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                loadTasks[i] = UnloadScene(names[i], n => progresses[index] = n, token: token);
            }

            StringBuilder debugProgress = new StringBuilder();
            // ロード中の進捗率を計算して通知。
            while (!token.IsCancellationRequested)
            {
                // 全てのシーンの平均進捗率を計算。
                float totalProgress = 0f;
                for (int i = 0; i < progresses.Length; i++)
                {
                    totalProgress += progresses[i];
                }
                float averageProgress = totalProgress / progresses.Length;
                loadingAction?.Invoke(averageProgress);

                #region デバッグ用に各シーンの進捗率をログ出力。
                debugProgress.Clear();
                debugProgress.AppendLine($"AverageProgress : {averageProgress}");
                for (int i = 0; i < progresses.Length; i++)
                {
                    debugProgress.Append($"\n  Scene {names[i]} Progress : {progresses[i]}");
                }
                Debug.Log(debugProgress.ToString());
                #endregion

                // 全てのシーンのロードが完了したか確認。
                bool allDone = true;
                for (int i = 0; i < loadTasks.Length; i++)
                {
                    if (!loadTasks[i].IsCompleted)
                    {
                        allDone = false;
                        break;
                    }
                }

                // 全てのシーンのアンロードが完了した場合、ループを抜ける。
                if (allDone) { break; }
                await Awaitable.NextFrameAsync(token);
            }

            if (token.IsCancellationRequested)
            {
                return false;
            }

            for (int i = 0; i < loadTasks.Length; i++)
            {
                if (!loadTasks[i].Result) { return false; }
            }

            return true;
        }

        /// <summary> Unityで現在ロードされているシーンから追跡データを再構築する。 </summary>
        internal void ResetSceneData()
        {
            int sceneCount = SceneManager.sceneCount;
            KeyValuePair<string, Scene>[] kvps = new KeyValuePair<string, Scene>[sceneCount];
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                kvps[i] = new KeyValuePair<string, Scene>(scene.name, scene);
            }

            _data.Reset(kvps);
        }

        /// <summary> 指定された初期シーンを順次ロードし、すべての完了を待機する。 </summary>
        internal async ValueTask InitializeLoad(string[] names)
        {
            ValueTask<bool>[] initializeTasks = new ValueTask<bool>[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                string sceneName = names[i];
                initializeTasks[i] = LoadScene(sceneName);
            }

            foreach (var task in initializeTasks)
            {
                await task;
            }
        }

        /// <summary> ルートオブジェクトへの依存注入と非同期初期化を文脈付きで実行する。 </summary>
        /// <param name="sceneName"> 初期化中のシーン名。 </param>
        /// <param name="rootObject"> 初期化するルートGameObject。 </param>
        /// <param name="injectable"> 依存注入を受ける実装。未実装の場合はnull。 </param>
        /// <param name="initializer"> 非同期初期化を行う実装。未実装の場合はnull。 </param>
        private static async Task InitializeRootObjectAsync(
            string sceneName,
            GameObject rootObject,
            IInjectable injectable,
            IInitializeAsync initializer)
        {
            if (injectable != null)
            {
                try
                {
                    ServiceInjector.TryAutoInject(injectable);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new SceneInitializationException(
                        sceneName,
                        rootObject.name,
                        injectable.GetType(),
                        ex);
                }
            }

            if (initializer == null)
            {
                return;
            }

            try
            {
                await initializer.DoInitialize();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SceneInitializationException(
                    sceneName,
                    rootObject.name,
                    initializer.GetType(),
                    ex);
            }
        }

        /// <summary> Unityシーンが有効かつロード済みで、名前を持つか確認する。 </summary>
        private static bool IsLoadedScene(Scene scene) =>
            scene.IsValid()
            && scene.isLoaded
            && !string.IsNullOrWhiteSpace(scene.name);

        private readonly SceneLoadData _data;

        /// <summary> 指定シーンを残し、それ以外の追跡シーンをアンロードする。 </summary>
        private async ValueTask ResetScene(params string[] scenesToKeep)
        {
            List<string> unloadScenes = new();
            foreach (var kvp in _data.SceneDict)
            {
                if (scenesToKeep.Contains(kvp.Key)) { continue; }
                unloadScenes.Add(kvp.Key);
            }

            await UnloadScenes(unloadScenes.ToArray());
        }
    }
}
