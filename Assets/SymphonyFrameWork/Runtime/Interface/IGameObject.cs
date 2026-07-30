using System.Threading;
using UnityEngine;

namespace SymphonyFrameWork
{
    /// <summary> GameObjectを所有するUnityオブジェクトの共通参照を公開する。 </summary>
    public interface IGameObject
    {
        /// <summary> 所有しているGameObject。 </summary>
        GameObject gameObject { get; }

        /// <summary> 所有しているTransform。 </summary>
        Transform transform { get; }

        /// <summary> 所有GameObjectの破棄時にキャンセルされるトークン。 </summary>
        CancellationToken destroyCancellationToken { get; }
    }
}
