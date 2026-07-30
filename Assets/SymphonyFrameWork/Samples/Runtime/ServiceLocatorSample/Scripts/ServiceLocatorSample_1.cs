using SymphonyFrameWork.System.ServiceLocate;
using UnityEngine;

namespace SymphonyFrameWork.Samples.ServiceLocatorSample
{
    /// <summary> 有効化時に自身をSingletonとして登録するService Locatorサンプル。 </summary>
    public sealed class ServiceLocatorSample_1 : MonoBehaviour
    {
        /// <summary> 重複を避けて自身をService Locatorへ登録する。 </summary>
        private void OnEnable()
        {
            if (ServiceLocator.IsExistInstance<ServiceLocatorSample_1>())
            {
                Debug.Log("ServiceLocatorSample_1 instance already exists.");
                return;
            }

            ServiceLocator.RegisterInstance(this, LocateType.Singleton);
        }
    }
}
