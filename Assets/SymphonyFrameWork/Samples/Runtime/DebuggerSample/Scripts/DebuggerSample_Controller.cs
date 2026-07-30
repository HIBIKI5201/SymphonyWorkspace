using SymphonyFrameWork.Debugger;
using SymphonyFrameWork.Debugger.HUD;
using SymphonyFrameWork.Debugger.Logger;
using SymphonyFrameWork.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SymphonyFrameWork.Samples.DebuggerSample
{
    /// <summary> SymphonyDebugLogger、SymphonyDebugHUD、SymphonyStopWatchの使い方を実演する。 </summary>
    public sealed class DebuggerSample_Controller : MonoBehaviour
    {
        private const string WORKLOAD_STOPWATCH_ID = "DebuggerSample_Workload";
        private const string UNKNOWN_STOPWATCH_ID = "DebuggerSample_NotStarted";
        private const int WORKLOAD_ITERATION_COUNT = 20000;
        private const float TOAST_DURATION_SECONDS = 3f;
        private const int COMMENTARY_CAPACITY = 12;

        /// <summary> HUDの上にパネルが重ならないよう、画面上部を空ける割合。 </summary>
        private const float HUD_RESERVED_HEIGHT_RATIO = 0.28f;

        [SerializeField, Tooltip("同じGameObjectに付いたHUD登録用コンポーネント。")]
        private DebuggerSample_HudProbe _hudProbe;

        private readonly Queue<string> _commentaryLogs = new();
        private Vector2 _scrollPosition;
        private int _pendingLogLineCount;
        private bool _hasHudProbe;
        private bool _isToastRunning;

        /// <summary> HUD登録用コンポーネントの参照を確認する。 </summary>
        private void Awake()
        {
            _hasHudProbe = !_hudProbe.LogAndCheckComponentNull();
        }

        /// <summary> サンプルの前提条件を実況ログへ出力する。 </summary>
        private void Start()
        {
            AddCommentary("サンプルを開始しました。各ボタンの結果はUnityのConsoleとGame Viewの両方で確認できます。");
            AddCommentary("SymphonyStopWatchと~ForEditor系APIはEditor限定です。Playerビルドでは呼び出しごと消えます。");
        }

        /// <summary> 通常のログを直接出力する。 </summary>
        [ContextMenu("Log Normal")]
        public void LogNormal()
        {
            SymphonyDebugLogger.LogDirect("DebuggerSampleからの通常ログです。", SymphonyDebugLogger.LogKind.Normal, this);
            AddCommentary("LogDirectでLogKind.Normalのログを出力しました。");
        }

        /// <summary> 警告のログを直接出力する。 </summary>
        [ContextMenu("Log Warning")]
        public void LogWarning()
        {
            SymphonyDebugLogger.LogDirect("DebuggerSampleからの警告ログです。", SymphonyDebugLogger.LogKind.Warning, this);
            AddCommentary("LogDirectでLogKind.Warningのログを出力しました。");
        }

        /// <summary> エラーのログを直接出力する。 </summary>
        [ContextMenu("Log Error")]
        public void LogError()
        {
            SymphonyDebugLogger.LogDirect("DebuggerSampleからのエラーログです。", SymphonyDebugLogger.LogKind.Error, this);
            AddCommentary("LogDirectでLogKind.Errorのログを出力しました。");
        }

        /// <summary> Editorでのみ出力されるログを試す。 </summary>
        [ContextMenu("Log For Editor Only")]
        public void LogForEditorOnly()
        {
            SymphonyDebugLogger.LogDirectForEditor("DebuggerSampleからのEditor限定ログです。");
            AddCommentary("LogDirectForEditorを呼びました。Editorでのみ出力され、ビルドでは呼び出しごと消えます。");
        }

        /// <summary> 出力待ちのログへ1行追加する。 </summary>
        [ContextMenu("Add Log Text")]
        public void AddLogText()
        {
            _pendingLogLineCount++;
            SymphonyDebugLogger.AddText($"蓄積された{_pendingLogLineCount}行目のメッセージです。");
            AddCommentary($"AddTextで{_pendingLogLineCount}行目を蓄積しました。Flushするまで出力されません。");
        }

        /// <summary> 出力待ちのログを破棄して新しく作り直す。 </summary>
        [ContextMenu("New Log Text")]
        public void NewLogText()
        {
            SymphonyDebugLogger.NewText("蓄積し直した1行目のメッセージです。");
            _pendingLogLineCount = 1;
            AddCommentary("NewTextで蓄積済みのログを破棄し、新しい1行目から作り直しました。");
        }

        /// <summary> 蓄積したログを1つのログとして出力する。 </summary>
        [ContextMenu("Flush Log Text")]
        public void FlushLogText()
        {
            if (_pendingLogLineCount <= 0)
            {
                AddCommentary("蓄積されたログがありません。先にAdd Log Textを押してください。");
                return;
            }

            SymphonyDebugLogger.LogText(SymphonyDebugLogger.LogKind.Normal, clearText: true, context: this);
            AddCommentary($"LogTextで蓄積した{_pendingLogLineCount}行を1つのログとして出力し、蓄積を破棄しました。");
            _pendingLogLineCount = 0;
        }

        /// <summary> 取得できない参照に対してnullチェック付きログを試す。 </summary>
        [ContextMenu("Check Null Component")]
        public void CheckNullComponent()
        {
            // このGameObjectにRendererは付いていないため、取得結果はnullになる。
            Renderer missingRenderer = GetComponent<Renderer>();
            bool isNull = missingRenderer.LogAndCheckComponentNull();
            AddCommentary($"LogAndCheckComponentNullの戻り値は {isNull} です。nullの場合のみ警告が出ます。");
        }

        /// <summary> HUDを表示する。 </summary>
        [ContextMenu("Show HUD")]
        public void ShowHud()
        {
            SymphonyDebugHUD.Show();
            AddCommentary("HUDを表示しました。FPSとメモリ使用量が画面左上に出ます。");
        }

        /// <summary> HUDを非表示にする。 </summary>
        [ContextMenu("Hide HUD")]
        public void HideHud()
        {
            SymphonyDebugHUD.Hide();

            // HideはHUDのGameObjectごと破棄するため、登録済みの追加テキストも一緒に失われる。
            if (_hasHudProbe)
            {
                _hudProbe.NotifyHudHidden();
            }

            AddCommentary("HUDを非表示にしました。登録済みの追加テキストも破棄されるため、再表示後は登録し直してください。");
        }

        /// <summary> HUDへ常時表示の追加テキストを登録する。 </summary>
        [ContextMenu("Register HUD Text")]
        public void RegisterHudText()
        {
            if (!_hasHudProbe)
            {
                AddCommentary($"{nameof(DebuggerSample_HudProbe)} が設定されていないため登録できません。");
                return;
            }

            AddCommentary(_hudProbe.Register()
                ? "AddText(Func<string>)で追加テキストを登録しました。HUDが非表示なら同時に生成されます。"
                : "登録済み、またはHUDが初期化されていないため登録できませんでした。");
        }

        /// <summary> HUDから追加テキストを解除する。 </summary>
        [ContextMenu("Unregister HUD Text")]
        public void UnregisterHudText()
        {
            if (!_hasHudProbe)
            {
                AddCommentary($"{nameof(DebuggerSample_HudProbe)} が設定されていないため解除できません。");
                return;
            }

            AddCommentary(_hudProbe.Unregister()
                ? "RemoveTextで追加テキストを解除しました。登録と解除は必ず対で行います。"
                : "未登録、またはHUDが初期化されていないため解除できませんでした。");
        }

        /// <summary> HUDへ一定時間だけ表示されるテキストを追加する。 </summary>
        [ContextMenu("Show HUD Toast")]
        public async void ShowHudToast()
        {
            if (_isToastRunning)
            {
                return;
            }

            _isToastRunning = true;
            AddCommentary($"AddText(string)で{TOAST_DURATION_SECONDS}秒だけ表示されるテキストを追加します。");

            try
            {
                await SymphonyDebugHUD.AddText(
                    "DebuggerSampleからの一時表示テキストです。",
                    TOAST_DURATION_SECONDS,
                    Color.cyan,
                    destroyCancellationToken);

                AddCommentary("一時表示テキストの表示時間が終了し、自動的に解除されました。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (SymphonyNotInitializedException)
            {
                AddCommentary("HUDが初期化されていないため一時表示できませんでした。");
            }
            finally
            {
                _isToastRunning = false;
            }
        }

        /// <summary> ストップウォッチで文字列連結の処理時間を計測する。 </summary>
        [ContextMenu("Measure Workload")]
        public void MeasureWorkload()
        {
            SymphonyStopWatch.Start(WORKLOAD_STOPWATCH_ID, "workload time is");
            RunWorkload();
            SymphonyStopWatch.Stop(WORKLOAD_STOPWATCH_ID);
            AddCommentary($"Start/Stopで{WORKLOAD_ITERATION_COUNT}回の文字列連結を計測しました。結果はConsoleに出ます。");
        }

        /// <summary> 開始していないIDを停止した場合の警告を確認する。 </summary>
        [ContextMenu("Stop Unknown Stopwatch")]
        public void StopUnknownStopwatch()
        {
            SymphonyStopWatch.Stop(UNKNOWN_STOPWATCH_ID);
            AddCommentary("開始していないIDをStopしました。Consoleへ警告が出ます。");
        }

        /// <summary> 各デバッグ機能の状態、操作ボタン、実況ログをIMGUIで描画する。 </summary>
        private void OnGUI()
        {
            // SymphonyHUDDrawerが画面左上から統計を描画するため、その領域を避けてパネルを置く。
            float margin = Mathf.Max(12f, Screen.width * 0.025f);
            float top = Mathf.Max(margin, Screen.height * HUD_RESERVED_HEIGHT_RATIO);
            float width = Screen.width - (margin * 2f);
            float height = Screen.height - top - margin;
            Rect outerRect = new(margin, top, width, height);
            Rect innerRect = new(
                outerRect.x + 12f,
                outerRect.y + 28f,
                outerRect.width - 24f,
                outerRect.height - 40f);

            GUI.Box(outerRect, "Debugger Sample");

            GUILayout.BeginArea(innerRect);
            GUILayout.Label("実況解説");
            GUILayout.Label("1. SymphonyStopWatchと~ForEditor系APIはEditor限定で、Playerビルドでは呼び出しごと消えます。");
            GUILayout.Label("2. LogDirectのログはEditorでのみパッケージ直下のCache/Log.txtへキャッシュ出力されます。");
            GUILayout.Label("3. Hide HUDは追加テキストの登録ごと破棄します。再表示後は登録し直してください。");
            GUILayout.Space(8f);

            GUILayout.Label($"Pending Log Lines   : {_pendingLogLineCount}");
            GUILayout.Label($"HUD Text Registered : {_hasHudProbe && _hudProbe.IsRegistered}");
            GUILayout.Label($"Toast State         : {(_isToastRunning ? "showing" : "idle")}");
            GUILayout.Space(8f);

            GUILayout.Label("SymphonyDebugLogger");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log Normal")) { LogNormal(); }
            if (GUILayout.Button("Log Warning")) { LogWarning(); }
            if (GUILayout.Button("Log Error")) { LogError(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Text")) { AddLogText(); }
            if (GUILayout.Button("New Text")) { NewLogText(); }
            if (GUILayout.Button("Flush Log Text")) { FlushLogText(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Check Null Component")) { CheckNullComponent(); }
            if (GUILayout.Button("Log For Editor Only")) { LogForEditorOnly(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.Label("SymphonyDebugHUD");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show HUD")) { ShowHud(); }
            if (GUILayout.Button("Hide HUD")) { HideHud(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Register HUD Text")) { RegisterHudText(); }
            if (GUILayout.Button("Unregister HUD Text")) { UnregisterHudText(); }
            if (GUILayout.Button($"Show Toast ({TOAST_DURATION_SECONDS:F0}s)")) { ShowHudToast(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.Label("SymphonyStopWatch");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Measure Workload")) { MeasureWorkload(); }
            if (GUILayout.Button("Stop Unknown Id")) { StopUnknownStopwatch(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Commentary Log");
            float logHeight = Mathf.Max(120f, innerRect.height * 0.25f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(logHeight));
            GUILayout.TextArea(BuildCommentaryText(), GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> 計測対象として、一定回数の文字列連結を行う。 </summary>
        private void RunWorkload()
        {
            StringBuilder builder = new();
            for (int i = 0; i < WORKLOAD_ITERATION_COUNT; i++)
            {
                builder.Append(i % 10);
            }
        }

        /// <summary> 表示上限を維持しながら実況ログを末尾へ追加する。 </summary>
        private void AddCommentary(string message)
        {
            if (_commentaryLogs.Count >= COMMENTARY_CAPACITY)
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
