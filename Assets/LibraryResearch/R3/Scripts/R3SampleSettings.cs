using UnityEngine;

namespace LibraryResearch.R3Sample
{
    [CreateAssetMenu(menuName = "Library Research/R3 Sample Settings")]
    public sealed class R3SampleSettings : ScriptableObject
    {
        [SerializeField] private int initialValue;
        [SerializeField] private int increment = 1;
        [SerializeField] private int milestone = 5;

        public int InitialValue => initialValue;
        public int Increment => increment;
        public int Milestone => milestone;
    }
}
