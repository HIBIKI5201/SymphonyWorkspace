using SymphonyFrameWork.Core;
using UnityEditor;
using UnityEngine;
using SymphonyFrameWork.Config;

namespace SymphonyFrameWork.Editor
{
    /// <summary> AudioManagerConfigとオーディオグループenumの再生成操作を描画する。 </summary>
    [CustomEditor(typeof(AudioManagerConfig))]
    public sealed class AudioManagerConfigDrawer : UnityEditor.Editor
    {
        /// <summary>
        /// InspectorのGUIを上書きします。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // AudioGroupTypeEnumを再生成するボタン。
            if (GUILayout.Button($"{EditorSymphonyConstant.AudioGroupTypeEnumName}Enumを再生成"))
            {
                AutoEnumGenerator.AudioEnumGenerate();
            }
        }

    }
}
