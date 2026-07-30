using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary> ロード対象シーンの状態、優先度、完了通知を保持する。 </summary>
    internal sealed class SceneLoadData
    {
        /// <summary> 空のシーン追跡データを生成する。 </summary>
        public SceneLoadData() { }

        /// <summary> 現在のアクティブシーン名と優先度。 </summary>
        public (string Name, int Priority) ActiveScene => _activeScene;

        /// <summary> 追跡中のシーン情報を読み取り専用で取得する。 </summary>
        internal ReadOnlyDictionary<string, SceneInfo> SceneDict => new(_sceneDict);

        /// <summary> 指定シーンをロード中として追跡へ追加する。 </summary>
        public void LoadStart(string name, int priority = 0)
        {
            _sceneDict.TryAdd(name, new(default, priority, SceneLoadState.Loading));
        }

        /// <summary> 指定シーンの参照を登録し、ロード完了へ遷移させる。 </summary>
        public void LoadComplete(string name, Scene scene)
        {
            if (!_sceneDict.TryGetValue(name, out SceneInfo info)) { return; }
            info.RegisterScene(scene);
            info.StateChange(SceneLoadState.Complete);
            _sceneDict[name] = info;
        }

        /// <summary> ロードに失敗したシーンを追跡から削除する。 </summary>
        public void LoadFail(string name)
        {
            _sceneDict.Remove(name);
        }

        /// <summary> 指定シーンをアンロード中へ遷移させる。 </summary>
        public void UnloadStart(string name)
        {
            if (!_sceneDict.TryGetValue(name, out SceneInfo info)) { return; }
            info.StateChange(SceneLoadState.Unloading);
            _sceneDict[name] = info;
        }

        /// <summary> アンロードを完了したシーンを追跡から削除する。 </summary>
        public void UnloadComplete(string name)
        {
            RemoveScene(name);
        }

        /// <summary> 現在のアクティブシーン名と優先度を記録する。 </summary>
        public void SetActiveScene(string name, int priority) => _activeScene = (name, priority);

        /// <summary> 空でない名前のシーンを追跡から削除する。 </summary>
        public void RemoveScene(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _sceneDict.Remove(name);
        }

        /// <summary> 指定シーンの追跡情報を追加または更新する。 </summary>
        public void UpsertScene(
            string name,
            Scene scene,
            int priority = 0,
            SceneLoadState state = SceneLoadState.Complete)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _sceneDict[name] = new SceneInfo(scene, priority, state);
        }

        /// <summary> 現在のUnityシーン一覧で追跡情報を再構築し、既存の優先度を引き継ぐ。 </summary>
        public void Reset(params KeyValuePair<string, Scene>[] newList)
        {
            Dictionary<string, SceneInfo> oldDict = new(_sceneDict);

            _sceneDict.Clear();

            foreach (KeyValuePair<string, Scene> pair in newList)
            {
                int priority = 0;

                if (oldDict.TryGetValue(pair.Key, out SceneInfo info))
                {
                    priority = info.Priority;
                }

                _sceneDict.Add(
                    pair.Key,
                    new SceneInfo(pair.Value, priority, SceneLoadState.Complete));
            }
        }

        /// <summary> シーン追跡、完了通知、アクティブシーン情報をすべて消去する。 </summary>
        internal void Clear()
        {
            _sceneDict.Clear();
            _loadedAction.Clear();
            _activeScene = default;
        }

        /// <summary> シーンのロード完了通知を登録し、完了済みの場合は即時実行する。 </summary>
        public void AddLoadedAction(string name, Action action)
        {
            // ロード済みなら即座に実行して終了。
            if (_sceneDict.TryGetValue(name, out SceneInfo info))
            {
                if (SceneLoadState.Complete <= info.State)
                {
                    action?.Invoke();
                    return;
                }
            }

            if (!_loadedAction.TryAdd(name, action))
            {
                _loadedAction[name] += action;
            }
        }

        /// <summary> 指定シーンのロード完了通知を一度だけ実行する。 </summary>
        public void InvokeLoadedAction(string name)
        {
            if (!_loadedAction.TryGetValue(name, out Action action)) { return; }
            action?.Invoke();
            _loadedAction.Remove(name);
        }

        /// <summary> 指定名のシーンが追跡中か確認する。 </summary>
        public bool IsExistScene(string name) =>
            !string.IsNullOrWhiteSpace(name) && _sceneDict.ContainsKey(name);

        /// <summary> 指定シーンのロード状態を取得する。 </summary>
        public bool TryGetSceneState(string name, out SceneLoadState state)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                state = SceneLoadState.None;
                return false;
            }

            if (!_sceneDict.TryGetValue(name, out SceneInfo info))
            {
                state = SceneLoadState.None;
                return false;
            }

            state = info.State;
            return true;
        }

        /// <summary> 指定シーンの追跡情報を取得する。 </summary>
        public bool TryGetSceneInfo(string name, out SceneInfo info)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                info = default;
                return false;
            }

            return _sceneDict.TryGetValue(name, out info);
        }

        /// <summary> ロード完了済みシーンのうち最も優先度が高い情報を取得する。 </summary>
        public bool TryGetHighestPriorityLoadedSceneInfo(out SceneInfo info)
        {
            info = default;
            bool found = false;

            foreach (KeyValuePair<string, SceneInfo> pair in _sceneDict)
            {
                SceneInfo current = pair.Value;
                if (!IsLoadedScene(current.Scene) || current.State < SceneLoadState.Complete)
                {
                    continue;
                }

                if (!found || info.Priority <= current.Priority)
                {
                    info = current;
                    found = true;
                }
            }

            return found;
        }

        /// <summary> シーン参照、ロード状態、アクティブ化優先度を保持する。 </summary>
        public struct SceneInfo
        {
            /// <summary> 指定したシーン情報から追跡データを生成する。 </summary>
            public SceneInfo(Scene scene, int priority = 0, SceneLoadState state = SceneLoadState.Loading)
            {
                _scene = scene;
                _state = state;
                _priority = priority;
            }

            /// <summary> 追跡対象のUnityシーン。 </summary>
            public Scene Scene => _scene;

            /// <summary> 現在のロード状態。 </summary>
            public SceneLoadState State => _state;

            /// <summary> アクティブシーン選択に使用する優先度。 </summary>
            public int Priority => _priority;

            /// <summary> ロード完了後のUnityシーン参照を登録する。 </summary>
            public void RegisterScene(Scene scene) => _scene = scene;

            /// <summary> 追跡中のロード状態を更新する。 </summary>
            public void StateChange(SceneLoadState state) => _state = state;

            private Scene _scene;
            private SceneLoadState _state;
            private int _priority;
        }

        private readonly Dictionary<string, SceneInfo> _sceneDict = new();
        private readonly Dictionary<string, Action> _loadedAction = new();

        private (string Name, int Priority) _activeScene;

        /// <summary> Unityシーンが有効かつロード済みで、名前を持つか確認する。 </summary>
        private static bool IsLoadedScene(Scene scene) =>
            scene.IsValid()
            && scene.isLoaded
            && !string.IsNullOrWhiteSpace(scene.name);
    }
}
