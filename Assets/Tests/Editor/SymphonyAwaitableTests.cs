using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using SymphonyFrameWork.Utility;

using UnityEngine;

namespace SymphonyFrameWork.Tests
{
    /// <summary> SymphonyAwaitableの完了、合成、キャンセル、timeout、Taskブリッジを検証する。 </summary>
    public sealed class SymphonyAwaitableTests
    {
        /// <summary> Completedが呼び出しごとに新しい完了済みAwaitableを返すことを検証する。 </summary>
        [Test]
        public async Task Completed_ReturnsFreshCompletedAwaitable()
        {
            Awaitable first = SymphonyAwaitable.Completed();
            Awaitable second = SymphonyAwaitable.Completed();

            Assert.That(ReferenceEquals(first, second), Is.False);

            await SymphonyAwaitable.AsTask(first);
            await SymphonyAwaitable.AsTask(second);
        }

        /// <summary> FromResultが呼び出しごとに新しいAwaitableと指定結果を返すことを検証する。 </summary>
        [Test]
        public async Task FromResult_ReturnsFreshAwaitableWithResult()
        {
            Awaitable<int> first = SymphonyAwaitable.FromResult(10);
            Awaitable<int> second = SymphonyAwaitable.FromResult(20);

            Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(await SymphonyAwaitable.AsTask(first), Is.EqualTo(10));
            Assert.That(await SymphonyAwaitable.AsTask(second), Is.EqualTo(20));
        }

        /// <summary> 結果なしWhenAllが空配列で正常完了することを検証する。 </summary>
        [Test]
        public async Task WhenAll_EmptyArray_Completes()
        {
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(Array.Empty<Awaitable>()));
        }

        /// <summary> 結果付きWhenAllが空配列で空の結果を返すことを検証する。 </summary>
        [Test]
        public async Task WhenAllGeneric_EmptyArray_ReturnsEmptyResults()
        {
            int[] results = await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(Array.Empty<Awaitable<int>>()));

            Assert.That(results, Is.Empty);
        }

        /// <summary> 結果なしWhenAllが同期完了したすべての要素を消費することを検証する。 </summary>
        [Test]
        public async Task WhenAll_CompletedAwaitables_Completes()
        {
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    SymphonyAwaitable.Completed(),
                    SymphonyAwaitable.Completed()));
        }

        /// <summary> 結果付きWhenAllが入力順に同期完了結果を返すことを検証する。 </summary>
        [Test]
        public async Task WhenAllGeneric_CompletedAwaitables_ReturnsOrderedResults()
        {
            int[] results = await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    SymphonyAwaitable.FromResult(3),
                    SymphonyAwaitable.FromResult(1),
                    SymphonyAwaitable.FromResult(2)));

            Assert.That(results, Is.EqualTo(new[] { 3, 1, 2 }));
        }

        /// <summary> 結果なしWhenAllが途中の例外後も残りを消費してから例外を通知することを検証する。 </summary>
        [Test]
        public void WhenAll_Exception_ConsumesRemainingAwaitables()
        {
            var failedSource = new AwaitableCompletionSource();
            var remainingSource = new AwaitableCompletionSource();
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    failedSource.Awaitable,
                    remainingSource.Awaitable));

            failedSource.SetException(new InvalidOperationException("failure"));
            Assert.That(task.IsCompleted, Is.False);

            remainingSource.SetResult();

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("failure"));
        }

        /// <summary> 結果付きWhenAllが例外をそのまま通知することを検証する。 </summary>
        [Test]
        public void WhenAllGeneric_Exception_PropagatesException()
        {
            Awaitable<int> failed = CreateFailedAwaitable<int>(
                new InvalidOperationException("generic failure"));
            Task<int[]> task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    SymphonyAwaitable.FromResult(1),
                    failed));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("generic failure"));
        }

        /// <summary> 結果なしWhenAllが要素キャンセルを通知することを検証する。 </summary>
        [Test]
        public void WhenAll_CanceledAwaitable_PropagatesCancellation()
        {
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    SymphonyAwaitable.Completed(),
                    CreateCanceledAwaitable()));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
        }

        /// <summary> 結果付きWhenAllが要素キャンセルを通知することを検証する。 </summary>
        [Test]
        public void WhenAllGeneric_CanceledAwaitable_PropagatesCancellation()
        {
            Task<int[]> task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(
                    SymphonyAwaitable.FromResult(1),
                    CreateCanceledAwaitable<int>()));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
        }

        /// <summary> 結果なしWhenAllがnull配列とnull要素を拒否することを検証する。 </summary>
        [Test]
        public void WhenAll_NullInput_ThrowsArgumentNullException()
        {
            Awaitable[] nullArray = null;
            Task nullArrayTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(nullArray));
            Task nullElementTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(new Awaitable[] { null }));

            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await nullArrayTask);
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await nullElementTask);
        }

        /// <summary> 結果付きWhenAllがnull配列とnull要素を拒否することを検証する。 </summary>
        [Test]
        public void WhenAllGeneric_NullInput_ThrowsArgumentNullException()
        {
            Awaitable<int>[] nullArray = null;
            Task<int[]> nullArrayTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(nullArray));
            Task<int[]> nullElementTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WhenAll(new Awaitable<int>[] { null }));

            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await nullArrayTask);
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await nullElementTask);
        }

        /// <summary> WaitWhileが最初からfalseの条件では同期的に完了することを検証する。 </summary>
        [Test]
        public async Task WaitWhile_ConditionFalse_CompletesImmediately()
        {
            int invocationCount = 0;
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitWhile(
                    () =>
                    {
                        invocationCount++;
                        return false;
                    }));

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        /// <summary> WaitWhileが条件評価中の例外を通知することを検証する。 </summary>
        [Test]
        public void WaitWhile_PredicateThrows_PropagatesException()
        {
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitWhile(
                    () => throw new InvalidOperationException("predicate failure")));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("predicate failure"));
        }

        /// <summary> WaitWhileが事前キャンセルを通知することを検証する。 </summary>
        [Test]
        public void WaitWhile_PreCanceled_PropagatesCancellation()
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitWhile(
                    () => false,
                    cancellationSource.Token));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
        }

        /// <summary> WaitUntilが最初からtrueの条件では同期的に完了することを検証する。 </summary>
        [Test]
        public async Task WaitUntil_ConditionTrue_CompletesImmediately()
        {
            int invocationCount = 0;
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitUntil(
                    () =>
                    {
                        invocationCount++;
                        return true;
                    }));

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        /// <summary> WaitUntilが事前キャンセルを通知することを検証する。 </summary>
        [Test]
        public void WaitUntil_PreCanceled_PropagatesCancellation()
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WaitUntil(
                    () => true,
                    cancellationSource.Token));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
        }

        /// <summary> 結果なしWithTimeoutが正常完了することを検証する。 </summary>
        [Test]
        public async Task WithTimeout_CompletedOperation_Completes()
        {
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    _ => SymphonyAwaitable.Completed(),
                    TimeSpan.FromSeconds(1)));
        }

        /// <summary> 結果付きWithTimeoutが正常結果を返すことを検証する。 </summary>
        [Test]
        public async Task WithTimeoutGeneric_CompletedOperation_ReturnsResult()
        {
            int result = await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    _ => SymphonyAwaitable.FromResult(42),
                    TimeSpan.FromSeconds(1)));

            Assert.That(result, Is.EqualTo(42));
        }

        /// <summary> 結果なしWithTimeoutがfactoryの例外を通知することを検証する。 </summary>
        [Test]
        public void WithTimeout_FactoryThrows_PropagatesException()
        {
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    _ => throw new InvalidOperationException("factory failure"),
                    TimeSpan.FromSeconds(1)));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("factory failure"));
        }

        /// <summary> 結果付きWithTimeoutがfactoryの例外を通知することを検証する。 </summary>
        [Test]
        public void WithTimeoutGeneric_FactoryThrows_PropagatesException()
        {
            Task<int> task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout<int>(
                    _ => throw new InvalidOperationException("generic factory failure"),
                    TimeSpan.FromSeconds(1)));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("generic factory failure"));
        }

        /// <summary> 結果なしWithTimeoutがlinked tokenのキャンセルをTimeoutExceptionへ変換することを検証する。 </summary>
        [Test]
        public void WithTimeout_ZeroTimeout_PropagatesTimeout()
        {
            bool receivedCanceledToken = false;
            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    token =>
                    {
                        receivedCanceledToken = token.IsCancellationRequested;
                        return CreateCanceledAwaitable();
                    },
                    TimeSpan.Zero));

            Assert.ThrowsAsync<TimeoutException>(async () => await task);
            Assert.That(receivedCanceledToken, Is.True);
        }

        /// <summary> 結果付きWithTimeoutがlinked tokenのキャンセルをTimeoutExceptionへ変換することを検証する。 </summary>
        [Test]
        public void WithTimeoutGeneric_ZeroTimeout_PropagatesTimeout()
        {
            bool receivedCanceledToken = false;
            Task<int> task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout<int>(
                    token =>
                    {
                        receivedCanceledToken = token.IsCancellationRequested;
                        return CreateCanceledAwaitable<int>();
                    },
                    TimeSpan.Zero));

            Assert.ThrowsAsync<TimeoutException>(async () => await task);
            Assert.That(receivedCanceledToken, Is.True);
        }

        /// <summary> 結果なしWithTimeoutが呼び出し側キャンセルをfactoryより先に通知することを検証する。 </summary>
        [Test]
        public void WithTimeout_PreCanceled_DoesNotInvokeFactory()
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            bool wasInvoked = false;

            Task task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    _ =>
                    {
                        wasInvoked = true;
                        return SymphonyAwaitable.Completed();
                    },
                    TimeSpan.FromSeconds(1),
                    cancellationSource.Token));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
            Assert.That(wasInvoked, Is.False);
        }

        /// <summary> 結果付きWithTimeoutが呼び出し側キャンセルをfactoryより先に通知することを検証する。 </summary>
        [Test]
        public void WithTimeoutGeneric_PreCanceled_DoesNotInvokeFactory()
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            bool wasInvoked = false;

            Task<int> task = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.WithTimeout(
                    _ =>
                    {
                        wasInvoked = true;
                        return SymphonyAwaitable.FromResult(1);
                    },
                    TimeSpan.FromSeconds(1),
                    cancellationSource.Token));

            Assert.CatchAsync<OperationCanceledException>(async () => await task);
            Assert.That(wasInvoked, Is.False);
        }

        /// <summary> 結果なしFromTaskが同期完了TaskをAwaitableへ変換することを検証する。 </summary>
        [Test]
        public async Task FromTask_CompletedTask_Completes()
        {
            await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(Task.CompletedTask));
        }

        /// <summary> 結果付きFromTaskが同期完了Taskの結果を返すことを検証する。 </summary>
        [Test]
        public async Task FromTaskGeneric_CompletedTask_ReturnsResult()
        {
            int result = await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(Task.FromResult(7)));

            Assert.That(result, Is.EqualTo(7));
        }

        /// <summary> 結果なしFromTaskが元Taskの例外を通知することを検証する。 </summary>
        [Test]
        public void FromTask_FaultedTask_PropagatesException()
        {
            Task bridgeTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(
                    Task.FromException(new InvalidOperationException("task failure"))));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await bridgeTask);
            Assert.That(exception.Message, Is.EqualTo("task failure"));
        }

        /// <summary> 結果付きFromTaskが元Taskの例外を通知することを検証する。 </summary>
        [Test]
        public void FromTaskGeneric_FaultedTask_PropagatesException()
        {
            Task<int> bridgeTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(
                    Task.FromException<int>(
                        new InvalidOperationException("generic task failure"))));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await bridgeTask);
            Assert.That(exception.Message, Is.EqualTo("generic task failure"));
        }

        /// <summary> 結果なしFromTaskが元Taskのキャンセルを通知することを検証する。 </summary>
        [Test]
        public void FromTask_CanceledTask_PropagatesCancellation()
        {
            var canceledToken = new CancellationToken(true);
            Task bridgeTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(Task.FromCanceled(canceledToken)));

            Assert.CatchAsync<OperationCanceledException>(
                async () => await bridgeTask);
        }

        /// <summary> 結果付きFromTaskが元Taskのキャンセルを通知することを検証する。 </summary>
        [Test]
        public void FromTaskGeneric_CanceledTask_PropagatesCancellation()
        {
            var canceledToken = new CancellationToken(true);
            Task<int> bridgeTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(
                    Task.FromCanceled<int>(canceledToken)));

            Assert.CatchAsync<OperationCanceledException>(
                async () => await bridgeTask);
        }

        /// <summary> FromTaskの待機キャンセルが元Taskを止めず、その後の例外をobserverが処理することを検証する。 </summary>
        [Test]
        public async Task FromTask_WaitCanceled_OriginalTaskContinuesAndIsObserved()
        {
            var originalSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationSource = new CancellationTokenSource();
            Task bridgeTask = SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromTask(
                    originalSource.Task,
                    cancellationSource.Token));

            cancellationSource.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await bridgeTask);
            Assert.That(originalSource.Task.IsCompleted, Is.False);

            originalSource.SetException(new InvalidOperationException("late failure"));
            await Task.Yield();

            Assert.That(originalSource.Task.IsFaulted, Is.True);
        }

        /// <summary> 結果なしAsTaskが正常完了AwaitableをTaskへ変換することを検証する。 </summary>
        [Test]
        public async Task AsTask_CompletedAwaitable_Completes()
        {
            await SymphonyAwaitable.AsTask(SymphonyAwaitable.Completed());
        }

        /// <summary> 結果付きAsTaskがAwaitableの結果をTaskへ変換することを検証する。 </summary>
        [Test]
        public async Task AsTaskGeneric_CompletedAwaitable_ReturnsResult()
        {
            int result = await SymphonyAwaitable.AsTask(
                SymphonyAwaitable.FromResult(11));

            Assert.That(result, Is.EqualTo(11));
        }

        /// <summary> 結果なしAsTaskがAwaitableの例外をTaskへ通知することを検証する。 </summary>
        [Test]
        public void AsTask_FaultedAwaitable_PropagatesException()
        {
            Task task = SymphonyAwaitable.AsTask(
                CreateFailedAwaitable(
                    new InvalidOperationException("awaitable failure")));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("awaitable failure"));
        }

        /// <summary> 結果付きAsTaskがAwaitableの例外をTaskへ通知することを検証する。 </summary>
        [Test]
        public void AsTaskGeneric_FaultedAwaitable_PropagatesException()
        {
            Task<int> task = SymphonyAwaitable.AsTask(
                CreateFailedAwaitable<int>(
                    new InvalidOperationException("generic awaitable failure")));

            InvalidOperationException exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.That(exception.Message, Is.EqualTo("generic awaitable failure"));
        }

        /// <summary> AsTaskの待機キャンセル後も元Awaitableをobserverが最後まで消費することを検証する。 </summary>
        [Test]
        public async Task AsTask_WaitCanceled_OriginalAwaitableContinuesAndIsObserved()
        {
            var originalSource = new AwaitableCompletionSource();
            using var cancellationSource = new CancellationTokenSource();
            Task task = SymphonyAwaitable.AsTask(
                originalSource.Awaitable,
                cancellationSource.Token);

            cancellationSource.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await task);

            originalSource.SetException(new InvalidOperationException("late failure"));
            await Task.Yield();

            Assert.That(task.IsCanceled, Is.True);
        }

        /// <summary> 結果付きAsTaskの待機キャンセル後も元Awaitableをobserverが最後まで消費することを検証する。 </summary>
        [Test]
        public async Task AsTaskGeneric_WaitCanceled_OriginalAwaitableContinuesAndIsObserved()
        {
            var originalSource = new AwaitableCompletionSource<int>();
            using var cancellationSource = new CancellationTokenSource();
            Task<int> task = SymphonyAwaitable.AsTask(
                originalSource.Awaitable,
                cancellationSource.Token);

            cancellationSource.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await task);

            int result = 5;
            originalSource.SetResult(result);
            await Task.Yield();

            Assert.That(task.IsCanceled, Is.True);
        }

        /// <summary> 結果なしの失敗済みAwaitableを作成する。 </summary>
        /// <param name="exception"> 完了時に通知する例外。 </param>
        /// <returns> 指定例外で完了したAwaitable。 </returns>
        private static Awaitable CreateFailedAwaitable(Exception exception)
        {
            var completionSource = new AwaitableCompletionSource();
            completionSource.SetException(exception);
            return completionSource.Awaitable;
        }

        /// <summary> 結果付きの失敗済みAwaitableを作成する。 </summary>
        /// <typeparam name="T"> 結果の型。 </typeparam>
        /// <param name="exception"> 完了時に通知する例外。 </param>
        /// <returns> 指定例外で完了したAwaitable。 </returns>
        private static Awaitable<T> CreateFailedAwaitable<T>(Exception exception)
        {
            var completionSource = new AwaitableCompletionSource<T>();
            completionSource.SetException(exception);
            return completionSource.Awaitable;
        }

        /// <summary> 結果なしのキャンセル済みAwaitableを作成する。 </summary>
        /// <returns> キャンセル済みAwaitable。 </returns>
        private static Awaitable CreateCanceledAwaitable()
        {
            var completionSource = new AwaitableCompletionSource();
            completionSource.SetCanceled();
            return completionSource.Awaitable;
        }

        /// <summary> 結果付きのキャンセル済みAwaitableを作成する。 </summary>
        /// <typeparam name="T"> 結果の型。 </typeparam>
        /// <returns> キャンセル済みAwaitable。 </returns>
        private static Awaitable<T> CreateCanceledAwaitable<T>()
        {
            var completionSource = new AwaitableCompletionSource<T>();
            completionSource.SetCanceled();
            return completionSource.Awaitable;
        }
    }
}
