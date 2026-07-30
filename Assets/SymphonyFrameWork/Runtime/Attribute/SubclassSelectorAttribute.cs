using System;
using UnityEngine;

namespace SymphonyFrameWork.Attribute
{
    /// <summary> シリアライズ参照へ代入できる派生型をインスペクターから選択可能にする。 </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
        /// <summary> MonoBehaviour派生型を候補に含めるか指定して属性を生成する。 </summary>
        /// <param name="includeMono"> MonoBehaviour派生型を候補に含める場合はtrue。 </param>
        public SubclassSelectorAttribute(bool includeMono = false)
        {
            _includeMono = includeMono;
        }

        /// <summary> MonoBehaviour派生型を候補に含めるかを取得する。 </summary>
        /// <returns> 候補に含める場合はtrue。 </returns>
        public bool IsIncludeMono() => _includeMono;

        private readonly bool _includeMono;
    }
}
