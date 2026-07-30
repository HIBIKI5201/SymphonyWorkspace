using SymphonyFrameWork.Attribute;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary> stringフィールドをBuild Settingsのシーン名選択欄として描画する。 </summary>
    [CustomPropertyDrawer(typeof(SceneNameSelectorAttribute))]
    public sealed class SceneNameSelectorDrawer : PropertyDrawer
    {
        /// <summary> Build Settingsのシーン名を選択するポップアップを描画する。 </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // string型のみ対応。
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "SceneNameSelectorはstring型にのみ使用できます。");
                return;
            }

            if (SceneList.Length == 0)
            {
                EditorGUI.LabelField(position, label.text, "ビルド設定にシーンが追加されていません。");
                return;
            }

            int index = Array.IndexOf(SceneList, property.stringValue);
            if (index < 0) index = 0;

            int selectedIndex = EditorGUI.Popup(position, label.text, index, SceneList);

            property.stringValue = SceneList[selectedIndex];
        }

        private string[] _sceneList;

        private string[] SceneList
        {
            get
            {
                if (_sceneList == null || _sceneList.Length != EditorBuildSettings.scenes.Length)
                {
                    _sceneList = EditorBuildSettings.scenes
                        .Select(s => Path.GetFileNameWithoutExtension(s.path))
                        .ToArray();
                }
                return _sceneList;
            }
        }
    }
}
