using UnityEngine;

namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    [CreateAssetMenu(menuName = "Library Research/SerializeReference Sample Data")]
    public sealed class SerializeReferenceSampleData : ScriptableObject
    {
        [SerializeReference, SubclassSelector]
        private INumberOperation operation = new AddOperation();

        public INumberOperation Operation => operation;
    }
}
