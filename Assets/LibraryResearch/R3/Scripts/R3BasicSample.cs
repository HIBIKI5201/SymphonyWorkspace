using R3;
using UnityEngine;

namespace LibraryResearch.R3Sample
{
    public sealed class R3BasicSample : MonoBehaviour
    {
        [SerializeField] private R3SampleSettings settings;

        private readonly ReactiveProperty<int> count = new ReactiveProperty<int>();
        private string derivedText = "Not subscribed";
        private string milestoneText = "Milestone not reached";

        private void Awake()
        {
            count.Value = settings.InitialValue;

            count
                .Select(value => $"Count x 2 = {value * 2}")
                .Subscribe(text => derivedText = text)
                .AddTo(this);

            count
                .Where(value => value >= settings.Milestone)
                .Subscribe(value => milestoneText = $"Milestone reached at {value}")
                .AddTo(this);
        }

        private void OnDestroy()
        {
            count.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24f, 24f, 420f, 230f), GUI.skin.box);
            GUILayout.Label("R3 Basic Sample");
            GUILayout.Label($"ReactiveProperty value: {count.Value}");
            GUILayout.Label(derivedText);
            GUILayout.Label(milestoneText);

            if (GUILayout.Button("Publish next value"))
            {
                count.Value += settings.Increment;
            }

            if (GUILayout.Button("Reset"))
            {
                milestoneText = "Milestone not reached";
                count.Value = settings.InitialValue;
            }

            GUILayout.EndArea();
        }
    }
}
