using SymphonyFramework.Attribute;
using UnityEngine;

namespace TestNameSpace
{
    public class TestIntarfaceInstance_1 : ITestInterface
    {
        public float X;
        public float Y;

        [SerializeReference, SubclassSelector]
        public IChildTestInterface ChildInterface;
    }
}
