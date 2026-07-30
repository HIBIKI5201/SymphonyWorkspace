using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymphonyFrameWork.Config;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary> SceneManagerConfigのシーン一覧をBuild Settingsから選択可能にする。 </summary>
    [CustomEditor(typeof(SceneManagerConfig))]
    public sealed class SceneManagerConfigDrawer: UnityEditor.Editor
    {
        private string[] _sceneNames;
        
        /// <summary> Inspector表示時にBuild Settingsのシーン名一覧をキャッシュする。 </summary>
        private void OnEnable()
        {
            _sceneNames = EditorBuildSettings.scenes
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToArray();
        }

        /// <summary> 起動時シーン設定の専用Inspectorを描画する。 </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var isResetAndLoadOnPlay = serializedObject.FindProperty("_isResetAndLoadOnPlay");
            var initializeSceneList = serializedObject.FindProperty("_initializeSceneList");

            var isResetAndLoadOnPlayLabel = new GUIContent(isResetAndLoadOnPlay.displayName, isResetAndLoadOnPlay.tooltip);
            isResetAndLoadOnPlay.boolValue = EditorGUILayout.Toggle(isResetAndLoadOnPlayLabel, isResetAndLoadOnPlay.boolValue);
            
            #region InitializeSceneList
            
            EditorGUILayout.LabelField("初期化時にロードするシーン（複数）", EditorStyles.boldLabel);
            
            //ポップアップを表示
            for (int i = 0; i < initializeSceneList.arraySize; i++)
            {
                var element = initializeSceneList.GetArrayElementAtIndex(i);
                int idx = Mathf.Max(0, Array.IndexOf(_sceneNames, element.stringValue));
                idx = EditorGUILayout.Popup((i + 1).ToString("00"), idx, _sceneNames);
                element.stringValue = _sceneNames[idx];
            }

            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("＋ シーンを追加"))
            {
                initializeSceneList.InsertArrayElementAtIndex(initializeSceneList.arraySize);
                initializeSceneList.GetArrayElementAtIndex(initializeSceneList.arraySize - 1).stringValue = _sceneNames.FirstOrDefault();
            }

            if (initializeSceneList.arraySize > 0 && GUILayout.Button("－ 最後のシーンを削除"))
            {
                initializeSceneList.DeleteArrayElementAtIndex(initializeSceneList.arraySize - 1);
            }
            
            GUILayout.EndHorizontal();
            
            #endregion

            serializedObject.ApplyModifiedProperties();
        }
    }
}
