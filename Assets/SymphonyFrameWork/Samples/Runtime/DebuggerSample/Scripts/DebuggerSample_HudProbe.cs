using SymphonyFrameWork.Debugger.HUD;
using SymphonyFrameWork.Exceptions;
using System;
using System.Text;
using UnityEngine;

namespace SymphonyFrameWork.Samples.DebuggerSample
{
    /// <summary> SymphonyDebugHUDへ追加テキストを登録・解除する対のパターンを実演する。 </summary>
    public sealed class DebuggerSample_HudProbe : MonoBehaviour
    {
        /// <summary> HUDへ追加テキストを登録済みならtrue。 </summary>
        public bool IsRegistered => _isRegistered;

        /// <summary>
        ///     HUDへ経過時間を表示する追加テキストを登録する。
        /// </summary>
        /// <returns> 登録できた場合はtrue。既に登録済み、またはHUD未初期化の場合はfalse。 </returns>
        public bool Register()
        {
            if (_isRegistered)
            {
                return false;
            }

            try
            {
                // AddTextはHUDの実体を遅延生成するため、Showを呼んでいなくてもここで表示される。
                SymphonyDebugHUD.AddText(_textFunc);
            }
            catch (SymphonyNotInitializedException)
            {
                // SymphonyOrchestratorの初期化前（Playモード外など）はHUDを操作できない。
                return false;
            }

            _isRegistered = true;
            return true;
        }

        /// <summary>
        ///     HUDから追加テキストを解除する。
        /// </summary>
        /// <returns> 解除できた場合はtrue。未登録、またはHUD未初期化の場合はfalse。 </returns>
        public bool Unregister()
        {
            if (!_isRegistered)
            {
                return false;
            }

            try
            {
                SymphonyDebugHUD.RemoveText(_textFunc);
            }
            catch (SymphonyNotInitializedException)
            {
                return false;
            }
            finally
            {
                _isRegistered = false;
            }

            return true;
        }

        /// <summary>
        ///     HUDが非表示にされたことを通知し、登録状態を解除済みへ戻す。
        ///     SymphonyDebugHUD.HideはHUDのGameObjectごと破棄するため、
        ///     登録済みのデリゲートも失われる。ここで状態を合わせておかないと、
        ///     後続のRemoveTextが解除目的でHUDを作り直してしまう。
        /// </summary>
        public void NotifyHudHidden()
        {
            _isRegistered = false;
        }

        private readonly StringBuilder _textBuilder = new();

        private Func<string> _textFunc;
        private bool _isRegistered;
        private float _elapsedSeconds;

        /// <summary> 登録と解除で同じデリゲートを渡せるよう、生成を1度だけにする。 </summary>
        private void Awake()
        {
            _textFunc = BuildHudText;
        }

        /// <summary> HUDへ表示する経過時間を進める。 </summary>
        private void Update()
        {
            _elapsedSeconds += Time.unscaledDeltaTime;
        }

        /// <summary> 登録したままオブジェクトが無効化されないよう解除する。 </summary>
        private void OnDisable()
        {
            Unregister();
        }

        /// <summary>
        ///     HUDへ毎フレーム表示する文字列を組み立てる。
        ///     毎フレーム評価されるため、StringBuilderを使い回して割り当てを抑える。
        /// </summary>
        /// <returns> HUDへ追加表示する1行。 </returns>
        private string BuildHudText()
        {
            _textBuilder.Clear();
            _textBuilder.Append("HUD Probe Elapsed : ");
            _textBuilder.Append(_elapsedSeconds.ToString("F1"));
            _textBuilder.Append(" s / Time Scale : ");
            _textBuilder.Append(Time.timeScale.ToString("F2"));
            return _textBuilder.ToString();
        }
    }
}
