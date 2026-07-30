using System;
using System.Threading;
using System.Threading.Tasks;

using SymphonyFrameWork.Core;
using SymphonyFrameWork.Exceptions;
using SymphonyFrameWork.Orchestrator;
using SymphonyFrameWork.System;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SymphonyFrameWork.Debugger.HUD
{
    /// <summary>
    ///     画面上にデバッグ用のHUDを表示するクラス
    /// </summary>
    public static class SymphonyDebugHUD
    {
        /// <summary>
        ///     HUDを表示する。
        /// </summary>
#if UNITY_EDITOR
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + nameof(SymphonyDebugHUD) + "/" + nameof(Show))]
#endif
        public static void Show()
        {
            EnsureInitialized();
            _ = _debugHUD.Value; // アクセスしてインスタンスを作成。
        }

        /// <summary>
        ///     HUDを非表示にする。
        /// </summary>
#if UNITY_EDITOR
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + nameof(SymphonyDebugHUD) + "/" + nameof(Hide))]
#endif
        public static void Hide()
        {
            EnsureInitialized();
            Initialize();
        }

        /// <summary>
        ///     SymphonyDebugHUDに追加のテキストを登録する。
        /// </summary>
        /// <param name="textFunc"> 毎フレーム表示文字列を返す処理。 </param>
        public static void AddText(Func<string> textFunc)
        {
            EnsureInitialized();

            if (textFunc == null)
            {
                throw new ArgumentNullException(nameof(textFunc));
            }

                _debugHUD.Value.Add(textFunc);
        }

        /// <summary>
        ///     SymphonyDebugHUDから追加のテキストを解除する。
        /// </summary>
        /// <param name="textFunc"> 解除する文字列生成処理。 </param>
        public static void RemoveText(Func<string> textFunc)
        {
            EnsureInitialized();

            if (textFunc == null)
            {
                throw new ArgumentNullException(nameof(textFunc));
            }

            _debugHUD.Value.Remove(textFunc);
        }

        /// <summary>
        ///     SymphonyDebugHUDに追加のテキストを表示する。
        /// </summary>
        /// <param name="text"> 一時表示する文字列。 </param>
        /// <param name="duration"> 表示を継続する秒数。 </param>
        /// <param name="color"> 文字へ適用する色。既定値の場合は色指定なし。 </param>
        /// <param name="token"> 表示待機を中断するためのトークン。 </param>
        public static async ValueTask AddText(string text, float duration = 3, Color color = default, CancellationToken token = default)
        {
            EnsureInitialized();

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (duration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "表示時間は0秒以上で指定してください。");
            }


            if (color != default) //カラーを指定する
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(color);
                text = $"<color=#{colorHex}>{text}</color>";
            }

            Func<string> textFunc = () => text;

            _debugHUD.Value.Add(textFunc);

            try
            {
                await Awaitable.WaitForSecondsAsync(duration, token);
            }
            finally
            {
                _debugHUD.Value.Remove(textFunc);
            }
        }

        /// <summary> 既存HUDを破棄し、遅延生成状態を初期化する。 </summary>
        internal static void Initialize()
        {
            // 破棄済み判定はSymphonyLazyObjectが担うため、ここでは前回の生成物を片付けるだけでよい。
            _debugHUD?.Destroy();

            _debugHUD = new SymphonyLazyObject<SymphonyHUDDrawer>(
                CreateDebugHUD,
                drawer => UnityEngine.Object.Destroy(drawer.gameObject));
        }

        private static SymphonyLazyObject<SymphonyHUDDrawer> _debugHUD;

        /// <summary> SymphonyのシステムオブジェクトとしてHUD描画コンポーネントを生成する。 </summary>
        /// <returns> 生成したHUD描画コンポーネント。 </returns>
        private static SymphonyHUDDrawer CreateDebugHUD()
        {
            return SymphonyOrchestrator.CreateSystemObject<SymphonyHUDDrawer>();
        }

        /// <summary> Debug HUDが利用可能な状態か検証する。 </summary>
        private static void EnsureInitialized()
        {
            if (_debugHUD == null)
            {
                throw new SymphonyNotInitializedException(typeof(SymphonyDebugHUD));
            }
        }
    }
}
