using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LibraryResearch.SRDebuggerSample
{
    public sealed class SRDebuggerBasicSample : MonoBehaviour
    {
        [SerializeField] private SRDebuggerSampleSettings settings;

        private Type srDebugType;
        private string status;

        private void Awake()
        {
            srDebugType = FindType("SRDebug");
            status = srDebugType == null
                ? "SRDebugger is not installed. Import it from My Assets."
                : $"Detected {srDebugType.FullName}.";

            if (settings.OpenPanelOnStart)
            {
                ShowPanel();
            }
        }

        private void ShowPanel()
        {
            if (srDebugType == null)
            {
                status = "Cannot open the panel until SRDebugger is installed.";
                return;
            }

            try
            {
                PropertyInfo instanceProperty = srDebugType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = instanceProperty?.GetValue(null);
                MethodInfo showMethod = instance?.GetType().GetMethod("ShowDebugPanel", Type.EmptyTypes);
                showMethod?.Invoke(instance, null);
                status = showMethod == null ? "ShowDebugPanel API was not found." : "Debug panel opened.";
            }
            catch (Exception exception)
            {
                status = $"SRDebugger invocation failed: {exception.GetBaseException().Message}";
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24f, 24f, 500f, 250f), GUI.skin.box);
            GUILayout.Label("SRDebugger Basic Sample");
            GUILayout.Label(status);

            if (GUILayout.Button("Open SRDebugger panel"))
            {
                ShowPanel();
            }

            if (GUILayout.Button("Write a sample log"))
            {
                Debug.Log(settings.SampleLogMessage);
            }

            GUILayout.EndArea();
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
