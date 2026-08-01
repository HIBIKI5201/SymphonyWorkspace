using System;
using UnityEngine;

namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    [Serializable]
    [AddTypeMenu("Math/Clamp")]
    public sealed class ClampOperation : INumberOperation
    {
        [SerializeField] private float minimum;
        [SerializeField] private float maximum = 100f;

        public string DisplayName => $"Clamp to [{minimum}, {maximum}]";
        public float Execute(float input) => Mathf.Clamp(input, minimum, maximum);
    }
}
