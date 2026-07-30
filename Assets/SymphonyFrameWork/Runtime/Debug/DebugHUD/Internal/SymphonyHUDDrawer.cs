using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace SymphonyFrameWork.Debugger.HUD
{
    /// <summary> フレーム統計と任意のデバッグ文字列を画面上へ描画する。 </summary>
    [DefaultExecutionOrder(-1000)]
    internal sealed class SymphonyHUDDrawer : MonoBehaviour
    {
        /// <summary> HUDへ毎フレーム評価する文字列生成処理を追加する。 </summary>
        /// <param name="func"> 表示文字列を返す処理。 </param>
        public void Add(Func<string> func) => _extraTexts.Add(func);

        /// <summary> HUDから文字列生成処理を削除する。 </summary>
        /// <param name="func"> 削除する文字列生成処理。 </param>
        public void Remove(Func<string> func) => _extraTexts.Remove(func);

        private readonly List<Func<string>> _extraTexts = new();
        private readonly StringBuilder _textToDisplay = new();

        private float _deltaTime = 0.0f;
        private Rect _rect;
        private GUIStyle _style;

        /// <summary> 現在の画面サイズに合わせてHUDの表示領域とスタイルを生成する。 </summary>
        private void Awake()
        {
            int w = Screen.width, h = Screen.height;

            Rect rect = new Rect(10, 10, w, h);

            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = h * 1 / 50;
            style.normal.textColor = Color.white;

            _rect = rect;
            _style = style;
        }

        /// <summary> フレーム統計と追加テキストの表示内容を更新する。 </summary>
        private void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f; // デルタタイムの計算（タイムスケールに影響しない）

            //基本テキストを取得。
            _textToDisplay.Clear();
            GetProfilingText(_textToDisplay);

            // 追加テキストを追加。
            _textToDisplay.AppendLine(GetExtraText());
        }

        /// <summary> 構築済みのHUD文字列を画面へ描画する。 </summary>
        private void OnGUI()
        {
            GUI.Label(_rect, _textToDisplay.ToString(), _style);
        }


        /// <summary> FPSとメモリ使用量を表示用文字列へ追加する。 </summary>
        /// <param name="text"> 統計情報の追加先。 </param>
        private void GetProfilingText(in StringBuilder text)
        {
            float msec;
            float fps;

            // デルタタイムが0以下の場合は、無効な値としてNaNを設定。
            if (_deltaTime <= 0f)
            {
                msec = float.NaN;
                fps = float.NaN;
            }
            else
            {
                msec = _deltaTime * 1000.0f;
                fps = 1.0f / _deltaTime;
            }

            long monoMemory = Profiler.GetMonoUsedSizeLong(); // Monoの使用メモリ量を取得。
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong(); // 総アロケートメモリ量を取得。
            long totalReserved = Profiler.GetTotalReservedMemoryLong(); // 総リザーブメモリ量を取得。

            text.AppendLine($"FPS: {fps.ToString("0.")} ({msec.ToString("0,0")} ms)");
            text.AppendLine($"Mono Memory: {GetMemoryUsageString(monoMemory)}");
            text.AppendLine($"Total Allocated: {GetMemoryUsageString(totalAllocated)}");
            text.AppendLine($"Total Reserved: {GetMemoryUsageString(totalReserved)}");
        }

        /// <summary> バイト数を読みやすい単位付き文字列へ変換する。 </summary>
        /// <param name="bytes"> 変換するバイト数。 </param>
        /// <returns> 適切な単位へ変換したメモリ量。 </returns>
        private string GetMemoryUsageString(long bytes)
        {
            if (bytes < 1024) { return $"{bytes} B"; }
            if (bytes < 1024 * 1024) { return $"{(bytes / 1024d):0.00} KB"; }
            if (bytes < 1024 * 1024 * 1024) { return $"{(bytes / (1024d * 1024d)):0.00} MB"; }
            return $"{(bytes / (1024d * 1024d * 1024d)):0.00} GB";
        }

        /// <summary> 登録されたすべての文字列生成処理を評価して結合する。 </summary>
        /// <returns> 改行で結合した追加テキスト。 </returns>
        private string GetExtraText()
        {
            StringBuilder extraTextBuilder = new();
            foreach (var textFunc in _extraTexts)
            {
                extraTextBuilder.AppendLine(textFunc());
            }
            return extraTextBuilder.ToString();
        }
    }
}
