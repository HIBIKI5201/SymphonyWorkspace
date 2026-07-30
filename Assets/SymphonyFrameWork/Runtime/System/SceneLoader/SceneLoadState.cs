using UnityEngine;

namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary> 追跡中シーンのロードおよびアンロード状態を表す。 </summary>
    public enum SceneLoadState : int
    {
        /// <summary> 追跡されていない状態。 </summary>
        None = -1,

        /// <summary> シーンをロードしている状態。 </summary>
        Loading = 0,

        /// <summary> シーンのロードが完了した状態。 </summary>
        Complete = 1,

        /// <summary> シーンをアンロードしている状態。 </summary>
        Unloading = 2
    }
}
