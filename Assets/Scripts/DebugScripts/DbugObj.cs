using SymphonyFramework;
using SymphonyFramework.Attribute;
using SymphonyFramework.Debugger;
using SymphonyFramework.Debugger.HUD;
using SymphonyFramework.SaveData;
using SymphonyFramework.Scenes;
using SymphonyFramework.Services;
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
        Partitura.Inject(this);

        SymphonyDebugHUD.AddText(() => $"time pp: {Time.time}");

        SceneLoad();
    }

    public void Inject(MeshRenderer meshRenderer)
    {
        Debug.Log(meshRenderer);
    }

    private async void SceneLoad()
    {
        await Attacca.LoadAsync("Scene2");
        Debug.Log("Scene2 loaded");

        await Awaitable.WaitForSecondsAsync(3);

        await Attacca.LoadAsync("Scene3");
        Debug.Log("Scene3 loaded");

        await Awaitable.WaitForSecondsAsync(3);

        await Attacca.UnloadAsync("Scene2");
        await Attacca.UnloadAsync("Scene3");
        Debug.Log("Scene2 and Scene3 unloaded");

        await Awaitable.WaitForSecondsAsync(3);

        await Attacca.LoadAsync("Scene2", priority: 10);
        await Attacca.LoadAsync("Scene3", priority: -5);
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