using System;
using SymphonyFramework.System.ServiceLocate;
using UnityEngine;

namespace BLINDED_AM_ME
{
    public class ServiceLocatorDebug : MonoBehaviour
    {
        private async void Start()
        {
            Debug.LogWarning(GC.GetTotalMemory(true));

            await Awaitable.WaitForSecondsAsync(5f);

            Debug.LogWarning(GC.GetTotalMemory(true));

            ServiceLocator.RegisterInstance(this);

            Debug.LogWarning(GC.GetTotalMemory(true));

            await Awaitable.WaitForSecondsAsync(5f);

            Destroy(gameObject);

        }



        private void Action() => Debug.Log("After Action called");
    }
}