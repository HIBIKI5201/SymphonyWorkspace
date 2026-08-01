using System;
using UnityEngine;

namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    [Serializable]
    [AddTypeMenu("Math/Add")]
    public sealed class AddOperation : INumberOperation
    {
        [SerializeField] private float amount = 10f;

        public string DisplayName => $"Add {amount}";
        public float Execute(float input) => input + amount;
    }
}
