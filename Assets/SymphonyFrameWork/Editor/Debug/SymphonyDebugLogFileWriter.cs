using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SymphonyFrameWork.Core;
using SymphonyFrameWork.Debugger.Logger;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor.Debugger.Logger
{
    /// <summary>
    ///     SymphonyDebugLogger.OnLogDirectを購読し、ログをファイルへキャッシュ出力する。
    /// </summary>
    [InitializeOnLoad]
    internal static class SymphonyDebugLogFileWriter
    {
        /// <summary> ログをファイルへ出力するかどうか。 </summary>
        public static bool IsFileLoggingEnabled = true;

        /// <summary> 一度に貯めるログが何件を超えたら即座に書き込むか。 </summary>
        private const int MAX_BUFFERED_LINES = 50;

        /// <summary> 貯めたログを書き込む間隔（秒）。 </summary>
        private const double FLUSH_INTERVAL_SECONDS = 5.0;

        /// <summary>
        ///     ログファイルの出力先。
        ///     SymphonyConstant.GetFrameworkAbsolutePath()でFramework自身の実配置パス
        ///     （Assets直置き、またはUPM経由のPackages/Library/PackageCache）を解決し、
        ///     その直下のCacheフォルダへ出力する。
        /// </summary>
        private static readonly string s_LogFilePath =
            Path.Combine(SymphonyConstant.GetFrameworkAbsolutePath(), "Cache", "Log.txt");

        private static readonly object s_FileLock = new();
        private static readonly List<string> s_PendingLines = new();
        private static Timer s_FlushTimer;

        static SymphonyDebugLogFileWriter()
        {
            SymphonyDebugLogger.OnLogDirect -= EnqueueLog;
            SymphonyDebugLogger.OnLogDirect += EnqueueLog;

            s_FlushTimer?.Dispose();
            s_FlushTimer = new Timer(
                _ => Flush(),
                null,
                TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS),
                TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS));

            Application.quitting -= Flush;
            Application.quitting += Flush;

            AssemblyReloadEvents.beforeAssemblyReload -= Flush;
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
        }

        /// <summary> ログをファイル出力用のバッファへ蓄積する。 </summary>
        private static void EnqueueLog(string text, SymphonyDebugLogger.LogKind kind)
        {
            if (!IsFileLoggingEnabled) return;

            lock (s_FileLock)
            {
                s_PendingLines.Add($"[{DateTime.Now:HH:mm:ss}][{kind}] {text}");

                if (s_PendingLines.Count >= MAX_BUFFERED_LINES) FlushLocked();
            }
        }

        /// <summary> 蓄積されたログをファイルへ書き込む。 </summary>
        private static void Flush()
        {
            lock (s_FileLock)
            {
                FlushLocked();
            }
        }

        /// <summary> s_FileLock取得済みの状態で呼び出すこと。 </summary>
        private static void FlushLocked()
        {
            if (s_PendingLines.Count == 0) return;

            try
            {
                string directory = Path.GetDirectoryName(s_LogFilePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                File.AppendAllLines(s_LogFilePath, s_PendingLines);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(SymphonyDebugLogFileWriter)}] ログファイルの書き込みに失敗しました。\n{e}");
            }

            s_PendingLines.Clear();
        }
    }
}
