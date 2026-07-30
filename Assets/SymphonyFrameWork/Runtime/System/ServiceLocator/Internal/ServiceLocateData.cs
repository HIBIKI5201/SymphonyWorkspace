using System;
using System.Collections.Generic;

using SymphonyFrameWork.Orchestrator;

using UnityEngine;

namespace SymphonyFrameWork.System.ServiceLocate
{
    /// <summary> Service Locatorの登録情報と登録待ちコールバックを保持する。 </summary>
    internal sealed class ServiceLocateData
    {
        /// <summary> Locator所有用GameObjectと空の登録状態を生成する。 </summary>
        public ServiceLocateData()
        {
            _gameObject = new GameObject("ServiceLocateData");
            SymphonyOrchestrator.PreserveObject(_gameObject);
        }

        /// <summary> Singleton登録されたComponentの所有元GameObject。 </summary>
        public GameObject Instance => _gameObject;

        /// <summary> 型をキーとする登録済みインスタンス一覧。 </summary>
        public Dictionary<Type, object> LocateObjects => _locateObjects;

        /// <summary> 型とインスタンスの組を重複なしで登録する。 </summary>
        public bool Add(Type type, object obj)
        {
            return _locateObjects.TryAdd(type, obj);
        }

        /// <summary> 登録インスタンスを解除し、Locator所有階層から切り離す。 </summary>
        public bool Remove(Type type)
        {
            if (_locateObjects.TryGetValue(type, out object obj))
            {
                if (
                    obj is Component component
#if UNITY_EDITOR
                    && !s_IsQuitting // エディタでランタイム終了時に起こるエラーの回避。
#endif
                    && component != null && !component.Equals(null) //nullチェックを行う
                    && component.transform.parent == _gameObject.transform) //親がロケーターのインスタンスか
                {
                    component.transform.SetParent(null);
                }

                _locateObjects.Remove(type);
                return true;
            }

            return false;
        }

        /// <summary> 指定型で登録されたインスタンスを取得する。 </summary>
        public T Get<T>()
        {
            if (_locateObjects.TryGetValue(typeof(T), out object value))
            {
                return (T)value;
            }

            return default;
        }

        /// <summary> 実行時型で登録されたインスタンスを取得する。 </summary>
        public object Get(Type type)
        {
            if (_locateObjects.TryGetValue(type, out object value))
            {
                return value;
            }

            return default;
        }

        /// <summary> 指定型のインスタンスが登録済みか確認する。 </summary>
        public bool IsLocate(Type type)
        {
            return _locateObjects.ContainsKey(type);
        }

        /// <summary> 指定型の登録後に引数なしで実行する処理を登録する。 </summary>
        public void RegisterAction<T>(Action action)
        {
            Type type = typeof(T);

            if (!_waitingActions.TryAdd(type, action))
            {
                _waitingActions[type] += action;
            }
        }

        /// <summary> 指定型の登録後にインスタンスを受け取る処理を登録する。 </summary>
        public void RegisterAction<T>(Action<T> action)
        {
            Type type = typeof(T);

            // まだ登録されていなければ、待機リストに追加します。
            if (_waitingActionsWithInstance.TryGetValue(type, out Delegate existing))
            {
                _waitingActionsWithInstance[type] = Delegate.Combine(existing, action);
            }
            else
            {
                _waitingActionsWithInstance[type] = action;
            }
        }

        /// <summary> 指定型の登録待ち処理を解除する。 </summary>
        /// <typeparam name="T"> 登録を待っていたサービス型。 </typeparam>
        /// <param name="action"> 解除する登録待ち処理。 </param>
        public void UnregisterAction<T>(Action<T> action)
        {
            Type type = typeof(T);

            if (!_waitingActionsWithInstance.TryGetValue(type, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, action);
            if (remaining == null)
            {
                _waitingActionsWithInstance.Remove(type);
                return;
            }

            _waitingActionsWithInstance[type] = remaining;
        }

        /// <summary> 指定型の登録を待っている処理を実行し、待機一覧から削除する。 </summary>
        public void InvokeWaitingAction(Type type, object instance)
        {
            // この型のインスタンスが登録されるのを待っていたアクションがあれば、ここで実行します。
            if (_waitingActions.TryGetValue(type, out Action waitingAction))
            {
                waitingAction?.Invoke();
                _waitingActions.Remove(type);
            }

            // 同様に、インスタンスを引数に取る待機アクションも実行します。
            if (_waitingActionsWithInstance
                .TryGetValue(type, out Delegate del))
            {
                del?.DynamicInvoke(instance);
                _waitingActionsWithInstance.Remove(type);
            }
        }

        /// <summary> 登録情報と未実行の待機コールバックをすべて消去する。 </summary>
        internal void Clear()
        {
            _locateObjects.Clear();
            _waitingActions.Clear();
            _waitingActionsWithInstance.Clear();
        }

        [Tooltip("登録されているインスタンスを型をキーにして保持する辞書")]
        private readonly Dictionary<Type, object> _locateObjects = new();

        [Tooltip("インスタンス登録まで待機してから実行されるコールバックアクションを保持する辞書")]
        private readonly Dictionary<Type, Action> _waitingActions = new();
        [Tooltip("インスタンス登録まで待機し、登録されたインスタンスを引数として受け取るコールバックアクションを保持する辞書")]
        private readonly Dictionary<Type, Delegate> _waitingActionsWithInstance = new();

        private readonly GameObject _gameObject;

        /// <summary> Locator所有用GameObjectが有効なロード済みシーンに存在するか確認する。 </summary>
        public bool IsValid() =>
            Instance != null && Instance.activeInHierarchy && Instance.scene.isLoaded;

#if UNITY_EDITOR // ランタイム終了時処理の対策。
        private static bool s_IsQuitting;

        /// <summary> Editorでの終了検知状態と購読をPlay Mode開始ごとに初期化する。 </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            s_IsQuitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        /// <summary> Unity終了中であることを記録し、破棄済みTransformへの操作を防ぐ。 </summary>
        private static void OnQuitting() => s_IsQuitting = true;
#endif
    }
}
