using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary>
    ///     String変数をシーンのリストから選択できるようにする。
    /// </summary>
    public sealed class SceneNameSelectorAttribute : PropertyAttribute
    {
        /// <summary> シーン名選択欄を表示する属性を生成する。 </summary>
        public SceneNameSelectorAttribute()
        {
        }
    }
}
