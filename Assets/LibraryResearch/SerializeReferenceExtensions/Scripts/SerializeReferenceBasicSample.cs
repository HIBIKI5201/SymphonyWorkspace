using UnityEngine;

namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    public sealed class SerializeReferenceBasicSample : MonoBehaviour
    {
        [SerializeField] private SerializeReferenceSampleData data;
        [SerializeField] private float input = 5f;

        private float result;

        private void Awake()
        {
            Execute();
        }

        private void Execute()
        {
            result = data.Operation?.Execute(input) ?? input;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24f, 24f, 460f, 250f), GUI.skin.box);
            GUILayout.Label("SerializeReferenceExtensions Basic Sample");
            GUILayout.Label("Change Data/SerializeReferenceSampleData.asset in the Inspector.");
            GUILayout.Label($"Selected operation: {data.Operation?.DisplayName ?? "None"}");
            GUILayout.Label($"Input: {input:0.##}  Result: {result:0.##}");

            input = GUILayout.HorizontalSlider(input, -20f, 100f);
            if (GUILayout.Button("Execute selected managed-reference operation"))
            {
                Execute();
            }

            GUILayout.EndArea();
        }
    }
}
