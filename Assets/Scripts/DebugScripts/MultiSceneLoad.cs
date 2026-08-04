using SymphonyFrameWork.System.SceneLoad;
using System.Linq;
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
            bool succeeded = await SceneLoader.LoadScenesAsync(scenes, loadingProgress =>
            {
                Debug.Log($"Loading Progress: {loadingProgress * 100}%");
            });

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
