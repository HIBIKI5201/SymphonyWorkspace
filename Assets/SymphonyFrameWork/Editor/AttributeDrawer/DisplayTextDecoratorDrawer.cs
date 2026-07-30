using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     文字の描画を行う
    /// </summary>
    [CustomPropertyDrawer(typeof(DisplayTextAttribute))]
    public sealed class DisplayTextDecoratorDrawer : DecoratorDrawer
    {
        private readonly GUIStyle Style = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontStyle = FontStyle.Normal,
            fontSize = 12
        };

        private DisplayTextAttribute DisplayTextAttribute => (DisplayTextAttribute)attribute;

        /// <summary> 表示文字列の行数に応じたDecorator領域の高さを返す。 </summary>
        public override float GetHeight()
        {
            //改行の数だけ高くする
            return Style.lineHeight * DisplayTextAttribute.Text.Split('\n').Length;
        }

        /// <summary> 属性に設定された文字列を中央揃えで描画する。 </summary>
        public override void OnGUI(Rect position)
        {
            // 指定した領域にテキストを表示する
            EditorGUI.LabelField(position, DisplayTextAttribute.Text, Style);
        }
    }
}
