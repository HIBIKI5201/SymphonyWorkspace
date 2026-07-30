using System;
using System.Threading;
using System.Threading.Tasks;
using SymphonyFrameWork.Debugger.Logger;
using SymphonyFrameWork.System;
using UnityEngine;

namespace SymphonyFrameWork.Utility
{
    /// <summary>
    ///     Tweenを提供する
    /// </summary>
    public static class SymphonyTween
    {
        /// <summary>
        ///     指定した時間の間、AnimationCurveかLerpな曲線で指定した範囲を毎フレーム実行する
        ///     curveを指定した場合はCurveで、指定しないかnullの場合はLerpで実行される
        /// </summary>
        /// <typeparam name="T"> 補間する値の型。 </typeparam>
        /// <param name="s">スタートの値</param>
        /// <param name="action">実行内容</param>
        /// <param name="e">エンドの値</param>
        /// <param name="d">長さ</param>
        /// <param name="curve">曲線を決める（xの大きさで正規化される）</param>
        /// <param name="token"> Tweenを中断するためのトークン。 </param>
        public static async Task Tweening<T>(T s, Action<T> action, T e, float d,
            AnimationCurve curve = null,
            CancellationToken token = default) where T : struct
        {
            curve = NormalizeCurve(curve);
            var timer = Time.time;

            //時間終了までループ
            while (Time.time <= timer + d)
            {
                var elapsed = Time.time - timer;

                var t = Mathf.Clamp01(elapsed / d); //正規化された値

                var result = curve != null ? CurveValue((s, e), t, curve) : LerpValue((s, e), t);

                if (result == null)
                {
                    SymphonyDebugLogger.LogDirect($"{typeof(T).Name}型は{nameof(Tweening)}に対応していません");
                    return;
                }

                action?.Invoke(result.Value);

                await Awaitable.NextFrameAsync(token);
            }

            //最後に最終値でやる
            action?.Invoke(e);
        }

        /// <summary>
        ///     指定した時間の間、AnimationCurveかLerpな曲線で指定した範囲を毎フレーム実行する
        ///     curveを指定した場合はCurveで、指定しないかnullの場合はLerpで実行される
        /// </summary>
        /// <typeparam name="T"> 補間する値の型。 </typeparam>
        /// <param name="s">スタートの値</param>
        /// <param name="action">実行内容</param>
        /// <param name="e">エンドの値</param>
        /// <param name="d">長さ</param>
        /// <param name="curve">曲線を決める（xの大きさで正規化される）</param>
        /// <param name="token"> Tweenを中断するためのトークン。 </param>
        public static async Task PausableTweening<T>(T s, Action<T> action, T e, float d,
            AnimationCurve curve = null,
            CancellationToken token = default) where T : struct
        {
            curve = NormalizeCurve(curve);
            var timer = Time.time;

            //時間終了までループ
            while (Time.time <= timer + d)
            {
                //ポーズが有効な時は更新しない
                if (PauseManager.Pause)
                {
                    timer += Time.deltaTime;
                    await Awaitable.NextFrameAsync(token);
                    continue;
                }

                var elapsed = Time.time - timer;

                var t = Mathf.Clamp01(elapsed / d); //正規化された値

                var result = curve != null ? CurveValue((s, e), t, curve) : LerpValue((s, e), t);

                if (result == null)
                {
                    SymphonyDebugLogger.LogDirect($"{typeof(T).Name}型は{nameof(Tweening)}に対応していません");
                    return;
                }

                action?.Invoke(result.Value);

                await Awaitable.NextFrameAsync(token);
            }

            //最後に最終値でやる
            action?.Invoke(e);
        }

        /// <summary>
        ///     指定した時間の間、Linearな曲線で指定した範囲を毎フレーム実行する
        /// </summary>
        /// <typeparam name="T"> 線形補間する値の型。 </typeparam>
        /// <param name="s">スタートの値</param>
        /// <param name="action">実行内容</param>
        /// <param name="e">エンドの値</param>
        /// <param name="d">長さ</param>
        /// <param name="token"> Tweenを中断するためのトークン。 </param>
        [Obsolete("旧型式です。" + nameof(Tweening) + "を使用する事を推奨します")]
        public static async Task TweeningLerp<T>(T s, Action<T> action, T e, float d,
            CancellationToken token = default) where T : struct
        {
            var timer = Time.time;

            //時間終了までループ
            while (Time.time <= timer + d)
            {
                var elapsed = Time.time - timer;

                var t = Mathf.Clamp01(elapsed / d); //正規化された値

                var result = LerpValue((s, e), t);

                if (result == null)
                {
                    SymphonyDebugLogger.LogDirect($"{typeof(T).Name}型は{nameof(TweeningLerp)}に対応していません");
                    return;
                }


                action?.Invoke(result.Value);

                await Awaitable.NextFrameAsync();
            }

            //最後に最終値でやる
            action?.Invoke(e);
        }

        /// <summary>
        ///     対応した型のLerpした値を返す
        /// </summary>
        /// <typeparam name="T"> 線形補間する値の型。 </typeparam>
        /// <param name="value"> 補間の開始値と終了値。 </param>
        /// <param name="t"> 0から1までの補間割合。 </param>
        /// <returns> 対応型の補間値。未対応型の場合はnull。 </returns>
        private static T? LerpValue<T>((T s, T e) value, float t) where T : struct
        {
            //それぞれの型でLerpを実行
            T? result = value switch
            {
                (int s, int e) => (T)Convert.ChangeType(Mathf.Lerp(s, e, t), typeof(T)),
                (float s, float e) => (T)Convert.ChangeType(Mathf.Lerp(s, e, t), typeof(T)),
                (Vector2 s, Vector2 e) => (T)Convert.ChangeType(Vector2.Lerp(s, e, t), typeof(T)),
                (Vector3 s, Vector3 e) => (T)Convert.ChangeType(Vector3.Lerp(s, e, t), typeof(T)),
                (Quaternion s, Quaternion e) => (T)Convert.ChangeType(Quaternion.Lerp(s, e, t), typeof(T)),
                (Color s, Color e) => (T)Convert.ChangeType(Color.Lerp(s, e, t), typeof(T)),
                _ => null
            };

            return result;
        }

        /// <summary>
        ///     指定した時間の間、Curveに対応した曲線で指定した範囲を毎フレーム実行する
        /// </summary>
        /// <typeparam name="T"> カーブ補間する値の型。 </typeparam>
        /// <param name="s">スタートの値</param>
        /// <param name="action">実行内容</param>
        /// <param name="e">エンドの値</param>
        /// <param name="d">長さ</param>
        /// <param name="curve"> 補間割合へ適用するAnimationCurve。 </param>
        /// <param name="token"> Tweenを中断するためのトークン。 </param>
        [Obsolete("旧型式です。" + nameof(Tweening) + "を使用する事を推奨します")]
        public static async Task TweeningCurve<T>(T s, Action<T> action, T e, float d, AnimationCurve curve,
            CancellationToken token = default) where T : struct
        {
            //カーブを
            curve = NormalizeCurve(curve);
            var timer = Time.time;

            //時間終了までループ
            while (Time.time <= timer + d)
            {
                var elapsed = Time.time - timer;

                var t = Mathf.Clamp01(elapsed / d); //正規化された値

                var result = CurveValue((s, e), t, curve);

                if (result == null)
                {
                    SymphonyDebugLogger.LogDirect($"{typeof(T).Name}型は{nameof(TweeningCurve)}に対応していません");
                    return;
                }


                action?.Invoke(result.Value);

                await Awaitable.NextFrameAsync();
            }

            //最後に最終値でやる
            action?.Invoke(e);
        }

        /// <summary>
        ///     対応した型のCurveのEvaluateした値を返す
        /// </summary>
        /// <typeparam name="T"> カーブ補間する値の型。 </typeparam>
        /// <param name="value"> 補間の開始値と終了値。 </param>
        /// <param name="t"> 0から1までの正規化時間。 </param>
        /// <param name="curve"> 補間割合へ適用するAnimationCurve。 </param>
        /// <returns> 対応型の補間値。未対応型の場合はnull。 </returns>
        private static T? CurveValue<T>((T s, T e) value, float t, AnimationCurve curve) where T : struct
        {
            //対応する型でカーブの量を掛ける
            T? result = value switch
            {
                (int s, int e) => (T)Convert.ChangeType((e - s) * curve.Evaluate(t), typeof(T)),
                (float s, float e) => (T)Convert.ChangeType((e - s) * curve.Evaluate(t), typeof(T)),
                (Vector2 s, Vector2 e) => (T)Convert.ChangeType((e - s) * curve.Evaluate(t), typeof(T)),
                (Vector3 s, Vector3 e) => (T)Convert.ChangeType((e - s) * curve.Evaluate(t), typeof(T)),
                (Color s, Color e) => (T)Convert.ChangeType((e - s) * curve.Evaluate(t), typeof(T)),
                _ => null
            };

            return result;
        }

        /// <summary>
        ///     カーブのキーをxが0~1になるように正規化する
        /// </summary>
        /// <param name="curve"> 正規化するAnimationCurve。 </param>
        /// <returns> 最後のキー時刻が1になるよう正規化した新しいカーブ。 </returns>
        private static AnimationCurve NormalizeCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return null;

            // 最後のキーの時間（x軸の最大値）を取得
            var maxTime = curve.keys[curve.length - 1].time;

            // 新しいカーブを作成
            var normalizedCurve = new AnimationCurve();

            // 各キーを正規化して新しいカーブに追加
            foreach (var key in curve.keys)
            {
                var normalizedTime = key.time / maxTime;
                var normalizedKey = new Keyframe(normalizedTime, key.value, key.inTangent, key.outTangent);
                normalizedCurve.AddKey(normalizedKey);
            }

            return normalizedCurve;
        }
    }
}
