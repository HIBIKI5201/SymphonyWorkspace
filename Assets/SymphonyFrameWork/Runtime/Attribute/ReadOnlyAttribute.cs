using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     インスペクター上で編集不可のプロパティを生成する
    /// </summary>
    public sealed class ReadOnlyAttribute : PropertyAttribute
    {
        /// <summary> 読み取り専用表示を指定する属性を生成する。 </summary>
        public ReadOnlyAttribute()
        {
        }
    }
}
