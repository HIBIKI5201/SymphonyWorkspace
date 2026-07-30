using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     インスペクターに文字を表示する
    /// </summary>
    public sealed class DisplayTextAttribute : PropertyAttribute
    {
        /// <summary>
        ///     インスペクターに表示する文字列を指定して属性を生成する。
        /// </summary>
        /// <param name="text"> インスペクターに表示する文字列。 </param>
        public DisplayTextAttribute(string text)
        {
            Text = text;
        }

        /// <summary> インスペクターに表示する文字列。 </summary>
        public string Text { get; private set; }
    }
}
