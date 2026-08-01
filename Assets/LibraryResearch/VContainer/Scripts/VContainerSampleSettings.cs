using UnityEngine;

namespace LibraryResearch.VContainerSample
{
    [CreateAssetMenu(menuName = "Library Research/VContainer Sample Settings")]
    public sealed class VContainerSampleSettings : ScriptableObject
    {
        [SerializeField] private string title = "VContainer constructor injection";
        [SerializeField] private int initialValue = 10;
        [SerializeField] private int increment = 5;

        public string Title => title;
        public int InitialValue => initialValue;
        public int Increment => increment;
    }
}
