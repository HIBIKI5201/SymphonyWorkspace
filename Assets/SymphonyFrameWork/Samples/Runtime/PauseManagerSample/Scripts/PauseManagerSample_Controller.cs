using SymphonyFrameWork.System;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SymphonyFrameWork.Samples.PauseManagerSample
{
    /// <summary> PauseManagerの状態切り替え、イベント通知、Pausable待機処理を実演する。 </summary>
    public sealed class PauseManagerSample_Controller : MonoBehaviour
    {
        private readonly Queue<string> _commentaryLogs = new();
        private Vector2 _scrollPosition;
        private int _unpausedFrameCount;
        private float _countdownRemaining;
        private bool _isCountdownRunning;

        /// <summary> ポーズ変更イベントを購読する。 </summary>
        private void OnEnable()
        {
            PauseManager.OnPauseChanged += OnPauseChanged;
        }

        /// <summary> ポーズ変更イベントの購読を解除する。 </summary>
        private void OnDisable()
        {
            PauseManager.OnPauseChanged -= OnPauseChanged;
        }

        /// <summary> サンプルを初期状態に戻し、フレームカウントループを開始する。 </summary>
        private void Start()
        {
            PauseManager.Pause = false;
            AddCommentary("サンプルを開始しました。Cubeは往復移動しながらPauseの影響を受けます。");
            CountFrames();
        }

        /// <summary> ポーズ状態をトグルする。 </summary>
        [ContextMenu("Toggle Pause")]
        public void TogglePause()
        {
            PauseManager.Pause = !PauseManager.Pause;
        }

        /// <summary> PausableWaitForSecondAsyncによるPause対応の5秒カウントダウンを開始する。 </summary>
        [ContextMenu("Start 5s Countdown")]
        public async void StartCountdown()
        {
            if (_isCountdownRunning)
            {
                return;
            }

            _isCountdownRunning = true;
            _countdownRemaining = 5f;
            AddCommentary("PausableWaitForSecondAsyncで5秒カウントダウンを開始します。Pause中は進みません。");

            try
            {
                while (_countdownRemaining > 0f)
                {
                    float step = Mathf.Min(0.1f, _countdownRemaining);
                    await PauseManager.PausableWaitForSecondAsync(step, destroyCancellationToken);
                    _countdownRemaining -= step;
                }

                AddCommentary("カウントダウンが完了しました。");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isCountdownRunning = false;
            }
        }

        /// <summary> ポーズ変更を実況ログへ記録する。 </summary>
        private void OnPauseChanged(bool paused)
        {
            AddCommentary(paused
                ? "Pauseへ切り替わりました。Cubeの移動とカウントが止まります。"
                : "Resumeへ切り替わりました。Cubeの移動とカウントが再開します。");
        }

        /// <summary> ポーズ状態、操作ボタン、実況ログをIMGUIで描画する。 </summary>
        private void OnGUI()
        {
            float margin = Mathf.Max(12f, Screen.width * 0.025f);
            float width = Screen.width - (margin * 2f);
            float height = Screen.height - (margin * 2f);
            Rect outerRect = new(margin, margin, width, height);
            Rect innerRect = new(
                outerRect.x + 12f,
                outerRect.y + 28f,
                outerRect.width - 24f,
                outerRect.height - 40f);

            GUI.Box(outerRect, "Pause Manager Sample");

            GUILayout.BeginArea(innerRect);
            GUILayout.Label("実況解説");
            GUILayout.Label("1. Cubeは往復移動し、PauseManager.Pauseがtrueの間だけ停止します。");
            GUILayout.Label("2. Unpaused FramesはPausableNextFrameAsyncで数え、Pause中は増加しません。");
            GUILayout.Label("3. カウントダウンはPausableWaitForSecondAsyncで進み、Pause中は進みません。");
            GUILayout.Space(8f);

            GUILayout.Label($"Pause State      : {(PauseManager.Pause ? "Paused" : "Running")}");
            GUILayout.Label($"Unpaused Frames  : {_unpausedFrameCount}");
            GUILayout.Label($"Countdown Remain : {_countdownRemaining:F1}s ({(_isCountdownRunning ? "running" : "idle")})");
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(PauseManager.Pause ? "Resume" : "Pause")) { TogglePause(); }
            if (GUILayout.Button("Start 5s Countdown")) { StartCountdown(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Commentary Log");
            float logHeight = Mathf.Max(180f, innerRect.height * 0.42f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(logHeight));
            GUILayout.TextArea(BuildCommentaryText(), GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> PausableNextFrameAsyncでポーズ中は増加しないフレームカウンタを回す。 </summary>
        private async void CountFrames()
        {
            try
            {
                while (true)
                {
                    await PauseManager.PausableNextFrameAsync(destroyCancellationToken);
                    _unpausedFrameCount++;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary> 表示上限を維持しながら実況ログを末尾へ追加する。 </summary>
        private void AddCommentary(string message)
        {
            if (_commentaryLogs.Count >= 12)
            {
                _commentaryLogs.Dequeue();
            }

            _commentaryLogs.Enqueue(message);
        }

        /// <summary> 現在の実況ログを改行区切りの表示文字列へまとめる。 </summary>
        private string BuildCommentaryText()
        {
            StringBuilder builder = new();
            foreach (string log in _commentaryLogs)
            {
                builder.AppendLine(log);
            }

            return builder.ToString();
        }
    }
}
