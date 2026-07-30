using SymphonyFrameWork.System.ServiceLocate;
using System;
using UnityEngine;

namespace SymphonyFrameWork.Samples.ServiceLocatorSample
{
    /// <summary> 遅延後に自身をLocatorとして登録する非同期サンプル。 </summary>
    public sealed class ServiceLocatorSample_2 : MonoBehaviour
    {
        /// <summary> GameObjectの破棄に追従する待機後、自身をService Locatorへ登録する。 </summary>
        private async void OnEnable()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(5f, destroyCancellationToken);
            }
            catch (Exception)
            {
                Debug.Log($"{gameObject.name}'s register is canceled");
                return;
            }

            ServiceLocator.RegisterInstance(this);
        }
    }
}
