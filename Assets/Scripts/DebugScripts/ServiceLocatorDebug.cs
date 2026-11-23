using System;
using SymphonyFramework.Services;
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

            Partitura.Register(this);

            Debug.LogWarning(GC.GetTotalMemory(true));

            await Awaitable.WaitForSecondsAsync(5f);

            Destroy(gameObject);

        }



        private void Action() => Debug.Log("After Action called");
    }
}