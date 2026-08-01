using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LibraryResearch.VContainerSample
{
    public sealed class VContainerBasicSample : LifetimeScope
    {
        [SerializeField] private VContainerSampleSettings settings;

        private string title = "Waiting for injection...";
        private int value;

        public event Action IncrementRequested;
        public event Action ResetRequested;

        public void Render(string newTitle, int newValue)
        {
            title = newTitle;
            value = newValue;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24f, 24f, 420f, 220f), GUI.skin.box);
            GUILayout.Label("VContainer Basic Sample");
            GUILayout.Label(title);
            GUILayout.Label($"Injected counter value: {value}");

            if (GUILayout.Button("Increment through injected service"))
            {
                IncrementRequested?.Invoke();
            }

            if (GUILayout.Button("Reset"))
            {
                ResetRequested?.Invoke();
            }

            GUILayout.EndArea();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(settings);
            builder.Register<ICounterService, CounterService>(Lifetime.Singleton);
            builder.RegisterInstance(this);
            builder.RegisterEntryPoint<VContainerSamplePresenter>();
        }
    }

}
