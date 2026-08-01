using UnityEngine;

namespace LibraryResearch.SRDebuggerSample
{
    [CreateAssetMenu(menuName = "Library Research/SRDebugger Sample Settings")]
    public sealed class SRDebuggerSampleSettings : ScriptableObject
    {
        [SerializeField] private bool openPanelOnStart;
        [SerializeField] private string sampleLogMessage = "Hello from the SRDebugger research scene.";

        public bool OpenPanelOnStart => openPanelOnStart;
        public string SampleLogMessage => sampleLogMessage;
    }
}
