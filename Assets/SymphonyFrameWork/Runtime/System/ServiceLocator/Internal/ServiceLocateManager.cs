using SymphonyFrameWork.Core;
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SymphonyFrameWork.System.ServiceLocate
{
    /// <summary> Service Locatorへの登録、解除、所有インスタンスの破棄を調停する。 </summary>
    internal sealed class ServiceLocateManager
    {
        /// <summary> 登録状態の保存先を指定して管理処理を生成する。 </summary>
        public ServiceLocateManager(ServiceLocateData data)
        {
            _data = data;
        }

        /// <summary>
        ///     指定されたインスタンスをロケーターに登録します。
        /// </summary>
        /// <param name="type"> 登録時のキーとして使用する実行時型。 </param>
        /// <param name="instance"> 登録するインスタンス。 </param>
        /// <param name="locateType"> SingletonまたはLocatorの登録方式。 </param>
        public bool RegisterInstance(Type type, object instance, LocateType locateType = LocateType.Singleton)
        {
            if (instance == null) { return false; }

            // 既に同じ型のインスタンスが登録されている場合は、新しいインスタンスを登録せずに処理を中断する。
            // 中断すると対象をDisposeして終了。
            if (!_data.Add(type, instance))
            {
                Dispose(instance);
                Debug.Log($"{type.Name}は既に登録されています。新しいインスタンスは破棄されました。");
                return false;
            }

#if UNITY_EDITOR
            // デバッグログ。
            if (EditorPrefs.GetBool(EditorSymphonyConstant.ServiceLocatorSetInstanceKey,
                EditorSymphonyConstant.ServiceLocatorSetInstanceDefault))
            {
                string instanceName = instance is Component c ? c.name : instance.GetType().Name;
                Debug.Log($"{type.Name}クラスの{instanceName}が{locateType switch { LocateType.Locator => "ロケート", LocateType.Singleton => "シングルトン", _ => string.Empty }}登録されました");
            }
#endif

            _data.InvokeWaitingAction(type, instance);

            // 登録タイプがSingletonで、かつインスタンスがComponentの場合、
            // ServiceLocatorのGameObjectの子要素にして、シーン内で管理しやすくします。
            if (locateType == LocateType.Singleton && instance is Component componentInstance)
            {
                componentInstance.transform.SetParent(_data.Instance.transform);
            }

            return true;
        }

        /// <summary>
        ///     指定した型のインスタンスをロケーターから登録解除します。
        /// </summary>
        /// <param name="type"> 登録解除する実行時型。 </param>
        /// <returns> 登録を解除できた場合はtrue。 </returns>
        public bool UnregisterInstance(Type type)
        {
            // 渡されたインスタンスが、指定された型で登録されているものと同一であるかを確認します。
            if (!_data.IsLocate(type))
            {
                Debug.LogWarning($"{type.Name}は登録されていません。");
                return false;
            }

            _data.Remove(type);

#if UNITY_EDITOR
            //ログを出力。
            if (EditorPrefs.GetBool(EditorSymphonyConstant.ServiceLocatorDestroyInstanceKey,
                EditorSymphonyConstant.ServiceLocatorDestroyInstanceDefault))
                Debug.Log($"{type.Name}が登録解除されました。");
#endif
            return true;
        }

        /// <summary>
        ///     指定した型のインスタンスを破棄します。
        /// </summary>
        /// <param name="type"> 破棄する登録済みインスタンスの実行時型。 </param>

        public bool DestroyInstance(Type type)
        {
            object instance = _data.Get(type);
            if (instance != null)
            {
                Dispose(instance);
                UnregisterInstance(type);
                return true;
            }

            return false;
        }


        private readonly ServiceLocateData _data;

        /// <summary> IDisposableまたはComponentとして登録インスタンスを解放する。 </summary>
        private bool Dispose(object instance)
        {
            bool isDispose = false;

            // IDisposableを実装していれば、Disposeメソッドを呼び出してリソースを解放します。
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
                isDispose = true;
            }

            if (instance is Component component)
            {
                UnityEngine.Object.Destroy(component.gameObject);
                isDispose = true;
            }

            return isDispose;
        }
    }
}
