using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SymphonyFrameWork.Debugger.Logger
{
    /// <summary>
    ///     UnityEditor上のみのログを発行する
    /// </summary>
    public static class SymphonyDebugLogger
    {
        /// <summary> 出力するログの重要度を表す。 </summary>
        public enum LogKind
        {
            /// <summary> 通常ログとして出力する。 </summary>
            Normal,

            /// <summary> 警告ログとして出力する。 </summary>
            Warning,

            /// <summary> エラーログとして出力する。 </summary>
            Error,
        }

        /// <summary>
        ///     直接出力されるデバッグログ。
        /// </summary>
        /// <param name="text"> 出力する文字列。 </param>
        /// <param name="kind"> 出力するログの重要度。 </param>
        /// <param name="context"> Consoleから追跡可能にするUnityオブジェクト。 </param>
        [HideInCallstack]
        public static void LogDirect(string text,
            LogKind kind = LogKind.Normal,
            UnityEngine.Object context = null)
        {
            switch (kind) 
            {
                case LogKind.Normal: Debug.Log(text, context); break;
                case LogKind.Warning: Debug.LogWarning(text, context); break;
                case LogKind.Error: Debug.LogError(text, context); break;
            }

            OnLogDirect?.Invoke(text, kind);
        }

        /// <summary>
        ///     LogDirectで出力されたログを外部へ通知する内部イベント。
        ///     ファイル出力など、Runtime層が関知すべきでない後続処理はEditor側の購読者に委ねる。
        /// </summary>
        internal static event Action<string, LogKind> OnLogDirect;

        /// <summary>
        ///     直接出力されるデバッグログ。
        ///     （エディタのみ）
        /// </summary>
        /// <param name="text"> Editorでのみ出力する文字列。 </param>
        /// <param name="kind"> 出力するログの重要度。 </param>
        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void LogDirectForEditor(string text, LogKind kind = LogKind.Normal)
        {
#if UNITY_EDITOR
            LogDirect(text, kind);
#endif
        }

        /// <summary>
        ///     追加されたメッセージをログに出力する。
        /// </summary>
        /// <param name="kind"> 出力するログの重要度。 </param>
        /// <param name="text"> 蓄積済み文字列の末尾へ追加する文字列。 </param>
        /// <param name="clearText"> 出力後に蓄積文字列を消去する場合はtrue。 </param>
        /// <param name="context"> Consoleから追跡可能にするUnityオブジェクト。 </param>
        [HideInCallstack]
        public static void LogText(LogKind kind = LogKind.Normal,
            string text = null,
            bool clearText = true,
            UnityEngine.Object context = null)
        {
            // ログが無ければ終了。
            if (_logTextBuilder == null) return;

            // 追加テキストがあれば追加。
            if (!string.IsNullOrEmpty(text)) _logTextBuilder.AppendLine(text);

            // ログビルダーを成形して出力。
            LogDirect(_logTextBuilder.ToString().TrimEnd(), kind, context);

            // クリアフラグがあればログを破棄。
            if (clearText) _logTextBuilder = null;
        }

        /// <summary>
        ///     追加されたメッセージをログに出力する。
        ///     （エディタのみ）
        /// </summary>
        /// <param name="kind"> 出力するログの重要度。 </param>
        /// <param name="text"> 蓄積済み文字列の末尾へ追加する文字列。 </param>
        /// <param name="clearText"> 出力後に蓄積文字列を消去する場合はtrue。 </param>
        /// <param name="context"> Consoleから追跡可能にするUnityオブジェクト。 </param>
        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void LogTextForEditor(LogKind kind = LogKind.Normal,
            string text = null,
            bool clearText = true,
            UnityEngine.Object context = null)
        {
#if UNITY_EDITOR
            LogText(kind, text, clearText, context);
#endif
        }

        /// <summary>
        ///     ログのテキストにメッセージを追加する。
        /// </summary>
        /// <param name="text"> 蓄積する文字列。 </param>
        public static void AddText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_logTextBuilder == null)
            {
                _logTextBuilder = new($"{text}\n"); // ログが無ければ新しく作る。
            }
            else
            {
                _logTextBuilder.AppendLine(text); // ログがあれば改行付きで追加。
            }
        }

        /// <summary>
        ///     ログのテキストにメッセージを追加する。
        ///     （エディタのみ）
        /// </summary>
        /// <param name="text"> Editorでのみ蓄積する文字列。 </param>
        [Conditional("UNITY_EDITOR")]
        public static void AddTextForEditor(string text)
        {
#if UNITY_EDITOR
            AddText(text);
#endif
        }

        /// <summary>
        ///     追加されたメッセージを削除し新しくする。
        /// </summary>
        /// <param name="text"> 初期値として新たに蓄積する文字列。 </param>
        public static void NewText(string text = null)
        {
            // ビルダーを破棄する。
            _logTextBuilder = null;

            // テキストがあれば追加する。
            AddText(text);
        }

        /// <summary>
        ///     追加されたメッセージを削除し新しくする。
        ///     （エディタのみ）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void NewTextForEditor(string text = null)
        {
#if UNITY_EDITOR
            NewText(text);
#endif
        }

        /// <summary>
        ///     コンポーネントがnullだった場合に警告を表示する。
        ///     戻り値にnullだったかを返す。
        /// </summary>
        /// <typeparam name="T"> nullを確認する参照型。 </typeparam>
        /// <param name="object"> nullを確認する対象。 </param>
        /// <returns>nullならtrue、nullではないならfalse</returns>
        [HideInCallstack]
        public static bool LogAndCheckComponentNull<T>(this T @object)
        {
            bool isNull = @object == null;

            if (isNull)
            {
                Debug.LogWarning($"<b>{typeof(T).Name}</b> is null");
            }

            return isNull;
        }

        /// <summary> ログを管理する </summary>
        private static StringBuilder _logTextBuilder = null;

        #region Obsolete機能
        /// <summary>
        ///     エディタ上でのみ出力されるデバッグログ
        /// </summary>
        /// <param name="text"> Editorでのみ出力する文字列。 </param>
        /// <param name="kind"> 出力するログの重要度。 </param>
        [Obsolete("この機能は旧型式です。" + nameof(LogDirect) + "を使用してください)")]
        [Conditional("UNITY_EDITOR")]
        public static void DirectLog(string text, LogKind kind = LogKind.Normal)
        {
#if UNITY_EDITOR
            LogDirect(text, kind);
#endif
        }

        /// <summary>
        ///     追加されたメッセージをログに出力する
        /// </summary>
        [Obsolete("この機能は旧型式です。" + nameof(LogText) + "を使用してください)")]
        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void TextLog(LogKind kind = LogKind.Normal, bool clearText = true)
        {
#if UNITY_EDITOR
            LogText(kind, clearText: clearText);
#endif
        }

        /// <summary>
        ///     コンポーネントだった場合に警告を表示する
        /// </summary>
        /// <typeparam name="T"> 確認するComponentの型。 </typeparam>
        /// <param name="component"> nullを確認するComponent。 </param>
        [Obsolete("この機能は旧型式です。" + nameof(LogAndCheckComponentNull) + "を使用してください。")]
        [Conditional("UNITY_EDITOR")]
        public static void CheckComponentNull<T>(this T component) where T : Component
        {
#if UNITY_EDITOR
            if (component == null) Debug.LogWarning($"The component {typeof(T).Name} of {component.name} is null.");
#endif
        }

        /// <summary> Componentが有効か確認し、nullの場合は警告を出力する旧API。 </summary>
        [Obsolete("この機能は安全性が保障されていません。" + nameof(LogAndCheckComponentNull) + "を使用してください")]
        public static bool IsComponentNotNull<T>(this T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogWarning($"The component of type {typeof(T).Name} is null.");
                return false;
            }

            return true;
        }
        #endregion
    }
}
