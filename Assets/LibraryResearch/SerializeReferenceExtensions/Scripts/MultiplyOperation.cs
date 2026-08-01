using System;
using UnityEngine;

namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    [Serializable]
    [AddTypeMenu("Math/Multiply")]
    public sealed class MultiplyOperation : INumberOperation
    {
        [SerializeField] private float multiplier = 2f;

        public string DisplayName => $"Multiply by {multiplier}";
        public float Execute(float input) => input * multiplier;
    }
}
