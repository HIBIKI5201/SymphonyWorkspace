using SymphonyFramework.Attribute;
using UnityEngine;
namespace TestNameSpace
{
    public class TestInterfaceInstance_2 : IChildTestInterface
    {
        public string Name;
        public int Age;

        [SerializeReference, SubclassSelector]
        public ITestInterface testInterface;
    }
}
