using SymphonyFrameWork.System;
using UnityEngine;

namespace SymphonyFrameWork.Samples.PauseManagerSample
{
    /// <summary> PauseManager.IPausableを実装し、Pause中は移動を止める往復移動オブジェクト。 </summary>
    public sealed class PauseManagerSample_Mover : MonoBehaviour, PauseManager.IPausable
    {
        [SerializeField, Tooltip("往復移動の中心からの最大距離。")]
        private float _range = 3f;

        [SerializeField, Tooltip("往復移動の速さ。")]
        private float _speed = 2f;

        private bool _paused;
        private float _elapsed;

        /// <summary> 往復移動を可視化するCubeを生成する。 </summary>
        private void Awake()
        {
            Transform visual = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            visual.SetParent(transform, worldPositionStays: false);
            visual.localPosition = Vector3.zero;
        }

        /// <summary> PauseManagerへポーズ通知の購読を登録する。 </summary>
        private void OnEnable()
        {
            PauseManager.IPausable.RegisterPauseManager(this);
        }

        /// <summary> PauseManagerへの購読を解除する。 </summary>
        private void OnDisable()
        {
            PauseManager.IPausable.UnregisterPauseManager(this);
        }

        /// <summary> Pause中でなければ往復移動を進める。 </summary>
        private void Update()
        {
            if (_paused)
            {
                return;
            }

            _elapsed += Time.deltaTime * _speed;
            float x = Mathf.Sin(_elapsed) * _range;
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }

        /// <summary> 移動を停止する。 </summary>
        public void Pause() => _paused = true;

        /// <summary> 移動を再開する。 </summary>
        public void Resume() => _paused = false;
    }
}
