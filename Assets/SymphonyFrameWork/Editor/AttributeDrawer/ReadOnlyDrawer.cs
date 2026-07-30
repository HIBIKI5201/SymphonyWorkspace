using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     プロパティを変更不可にする
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public sealed class ReadOnlyDrawer : PropertyDrawer
    {
        /// <summary> 対象プロパティを無効化した状態で描画する。 </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    /// <summary>
    ///     ReadOnryが表示されない場合に警告を出す
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    public sealed class ReadOnlyInspector : UnityEditor.Editor
    {
        /// <summary> ReadOnly属性のシリアライズ漏れを警告して既定Inspectorを描画する。 </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var targetType = target.GetType();
            var fields = targetType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
                if (field.IsDefined(typeof(ReadOnlyAttribute), true))
                {
                    var property = serializedObject.FindProperty(field.Name);
                    if (property == null)
                        Debug.LogWarning(
                            $"フィールド '{field.Name}' は [ReadOnly] 属性が付与されていますが、[SerializeField] 属性が付与されていないため、インスペクターに表示されません。");
                }

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
