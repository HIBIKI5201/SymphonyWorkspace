using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     String変数をタグのリストから選択できるようにする。
    /// </summary>
    public sealed class TagSelectorAttribute : PropertyAttribute
    {
        /// <summary> タグ選択欄を表示する属性を生成する。 </summary>
        public TagSelectorAttribute()
        {
        }
    }
}
