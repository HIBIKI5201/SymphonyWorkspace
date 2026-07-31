using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using SymphonyFrameWork.Utility;

using UnityEngine;
using UnityEngine.TestTools;

namespace SymphonyFrameWork.Tests
{
    /// <summary> フレーム進行とメインスレッドを必要とするSymphonyAwaitableの挙動を検証する。 </summary>
    public sealed class SymphonyAwaitableRuntimeTests
    {
        /// <summary> WithTimeoutが指定時間を超えて継続した処理へTimeoutExceptionを通知することを検証する。 </summary>
        [UnityTest]
        public IEnumerator WithTimeout_OperationExceedsDuration_PropagatesTimeout()
        {
            int frameCount = 0;
            float startedAt = Time.realtimeSinceStartup;
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    token => WaitUntilCanceledAsync(
                        token,
                        () => frameCount++),
                    TimeSpan.FromMilliseconds(100)));

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.Throws<TimeoutException>(
                () => task.GetAwaiter().GetResult());
            Assert.That(frameCount, Is.GreaterThan(0));
            Assert.That(
                Time.realtimeSinceStartup - startedAt,
                Is.GreaterThanOrEqualTo(0.05f));
        }

        /// <summary> WithTimeoutで呼び出し側キャンセルとtimeoutが同時成立した場合の優先順位を検証する。 </summary>
        [UnityTest]
        public IEnumerator WithTimeout_CallerCancellationAndTimeout_CallerCancellationWins()
        {
            using var cancellationSource = new CancellationTokenSource();
            bool receivedTimeoutCancellation = false;
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    token =>
                    {
                        receivedTimeoutCancellation = token.IsCancellationRequested;
                        cancellationSource.Cancel();
                        return CreateCanceledAwaitable();
                    },
                    TimeSpan.Zero,
                    cancellationSource.Token));

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.Catch<OperationCanceledException>(
                () => task.GetAwaiter().GetResult());
            Assert.That(receivedTimeoutCancellation, Is.True);
            Assert.That(cancellationSource.IsCancellationRequested, Is.True);
        }

        /// <summary> WaitWhileが待機開始後のキャンセルをOperationCanceledExceptionとして通知することを検証する。 </summary>
        [UnityTest]
        public IEnumerator WaitWhile_CanceledAfterWaiting_PropagatesCancellation()
        {
            using var cancellationSource = new CancellationTokenSource();
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitWhile(
                    () => true,
                    cancellationSource.Token));

            Assert.That(task.IsCompleted, Is.False);
            yield return null;

            cancellationSource.Cancel();
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.Catch<OperationCanceledException>(
                () => task.GetAwaiter().GetResult());
        }

        /// <summary> FromTaskの両オーバーロードがスレッドプール完了後にメインスレッドで再開することを検証する。 </summary>
        [UnityTest]
        public IEnumerator FromTask_ThreadPoolCompletion_ResumesOnMainThread()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var source = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task<int> continuationThreadTask =
                AwaitFromTaskAndGetContinuationThreadAsync(source.Task);

            _ = Task.Run(() => source.SetResult(true));
            yield return new WaitUntil(() => continuationThreadTask.IsCompleted);

            Assert.That(
                continuationThreadTask.GetAwaiter().GetResult(),
                Is.EqualTo(mainThreadId));

            var genericSource = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int bridgedResult = 0;
            Task<int> genericContinuationThreadTask =
                AwaitFromTaskAndGetContinuationThreadAsync(
                    genericSource.Task,
                    result => bridgedResult = result);

            _ = Task.Run(() => genericSource.SetResult(42));
            yield return new WaitUntil(
                () => genericContinuationThreadTask.IsCompleted);

            Assert.That(
                genericContinuationThreadTask.GetAwaiter().GetResult(),
                Is.EqualTo(mainThreadId));
            Assert.That(bridgedResult, Is.EqualTo(42));
        }

        /// <summary> キャンセルされるまでフレーム待機を継続する。 </summary>
        /// <param name="token"> 待機を中断するためのトークン。 </param>
        /// <param name="onFrame"> 各フレーム待機前に実行する処理。 </param>
        /// <returns> キャンセル時に完了するAwaitable。 </returns>
        private static async Awaitable WaitUntilCanceledAsync(
            CancellationToken token,
            Action onFrame)
        {
            while (true)
            {
                onFrame.Invoke();
                await Awaitable.NextFrameAsync(token);
            }
        }

        /// <summary> 結果なしFromTaskを待機し、再開したスレッドのIDを返す。 </summary>
        /// <param name="task"> スレッドプールで完了させるTask。 </param>
        /// <returns> FromTaskの待機後に実行しているスレッドのID。 </returns>
        private static async Task<int> AwaitFromTaskAndGetContinuationThreadAsync(
            Task task)
        {
            await SymphonyAwaitable.FromTask(task);
            return Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary> 結果付きFromTaskを待機し、再開したスレッドのIDを返す。 </summary>
        /// <param name="task"> スレッドプールで完了させる結果付きTask。 </param>
        /// <param name="resultObserver"> FromTaskから受け取った結果の通知先。 </param>
        /// <returns> FromTaskの待機後に実行しているスレッドのID。 </returns>
        private static async Task<int> AwaitFromTaskAndGetContinuationThreadAsync(
            Task<int> task,
            Action<int> resultObserver)
        {
            int result = await SymphonyAwaitable.FromTask(task);
            resultObserver.Invoke(result);
            return Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary> 結果なしのキャンセル済みAwaitableを作成する。 </summary>
        /// <returns> キャンセル済みAwaitable。 </returns>
        private static Awaitable CreateCanceledAwaitable()
        {
            var completionSource = new AwaitableCompletionSource();
            completionSource.SetCanceled();
            return completionSource.Awaitable;
        }
    }
}
