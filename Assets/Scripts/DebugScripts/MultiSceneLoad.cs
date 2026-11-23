using System;
using System.Linq;
using SymphonyFramework.Scenes;
using UnityEngine;

namespace TestNameSpace
{
    public class MultiSceneLoad : MonoBehaviour
    {
        [SerializeField]
        private SceneListEnum[] _sceneListEnums;

        async void Start()
        {
            return;
            string[] scenes = _sceneListEnums.Select(s => s.ToString()).ToArray();
            bool succeeded = await Attacca.LoadAsync(scenes, new Progress<float>(loadingProgress =>
            {
                Debug.Log($"Loading Progress: {loadingProgress * 100}%");
            }));

            if (succeeded)
            {
                Debug.Log("All scenes loaded successfully.");
            }
            else
            {
                Debug.LogError("Failed to load scenes.");
            }
        }

    }
}
