using SymphonyFrameWork;
using SymphonyFrameWork.Attribute;
using SymphonyFrameWork.Debugger;
using SymphonyFrameWork.Debugger.HUD;
using SymphonyFrameWork.System.SaveSystem;
using SymphonyFrameWork.System.SceneLoad;
using SymphonyFrameWork.System.ServiceLocate;
using System;
using TestNameSpace;
using UnityEngine;

public class DbugObj : MonoBehaviour, IGameObject, IInjectable<MeshRenderer>
{
    [SerializeField] private float _speed = 5;

    [DisplayText("試し書き試し書き試し書き")]
    [ReadOnly]
    [SerializeField]
    private Vector3 _velocity;
    [SerializeField, TagSelector]
    private string _tag;
    [SerializeField, SceneNameSelector]
    private string _sceneName;

    [SerializeField] private AnimationCurve _curve;

    [SerializeField]
    private Color _color;

    [SerializeReference, SubclassSelector]
    private ITestInterface _g;

    public void Start()
    {
        ServiceInjector.Inject(this);

        SymphonyDebugHUD.AddText(() => $"time pp: {Time.time}");

        SceneLoad();
    }

    public void Inject(MeshRenderer meshRenderer)
    {
        Debug.Log(meshRenderer);
    }

    private async void SceneLoad()
    {
        await SceneLoader.LoadSceneAsync("Scene2");
        Debug.Log("Scene2 loaded");

        await Awaitable.WaitForSecondsAsync(3);

        await SceneLoader.LoadSceneAsync("Scene3");
        Debug.Log("Scene3 loaded");

        await Awaitable.WaitForSecondsAsync(3);

        await SceneLoader.UnloadSceneAsync("Scene2");
        await SceneLoader.UnloadSceneAsync("Scene3");
        Debug.Log("Scene2 and Scene3 unloaded");

        await Awaitable.WaitForSecondsAsync(3);

        await SceneLoader.LoadSceneAsync("Scene2", priority: 10);
        await SceneLoader.LoadSceneAsync("Scene3", priority: -5);
    }

    [Serializable]
    private class TestData
    {
        public string Name { get; set; } = "TestName";

        public override string ToString()
        {
            return $"name is {Name}";
        }
    }
}