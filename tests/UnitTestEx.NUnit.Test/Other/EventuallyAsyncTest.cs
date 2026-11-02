#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using UnitTestEx.Abstractions;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class EventuallyAsyncTest
    {
        [Test]
        public async Task EventuallyAsync_ReturnsFirstResultSatisfyingCondition()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var attempts = 0;

            var result = await test.EventuallyAsync(
                _ => Task.FromResult(++attempts == 3 ? "ready" : null),
                value => value is not null,
                timeout: TimeSpan.FromSeconds(1),
                interval: TimeSpan.FromMilliseconds(1));

            Assert.That(result, Is.EqualTo("ready"));
            Assert.That(attempts, Is.EqualTo(3));
        }

        [Test]
        public void EventuallyAsync_ThrowsTimeoutWhenConditionNeverMatches()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var exception = Assert.ThrowsAsync<TimeoutException>(() => test.EventuallyAsync(
                _ => Task.FromResult("report blob not found"),
                value => value == "ready",
                timeout: TimeSpan.FromMilliseconds(20),
                interval: TimeSpan.FromMilliseconds(1)));

            Assert.That(exception!.Message, Does.Contain("Attempts: "));
            Assert.That(exception.Message, Does.Contain("Last observation: report blob not found"));
            Assert.That(spy.Lines.Any(line => line?.StartsWith("FAILURE > ", StringComparison.Ordinal) == true), Is.True);
        }

        [Test]
        public void EventuallyAsync_BoundsObservationAndPreservesActionException()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);
            var attempts = 0;
            var failure = new InvalidOperationException("download failed");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => test.EventuallyAsync(
                _ => ++attempts == 1 ? Task.FromResult(new string('x', 5000)) : throw failure,
                _ => false,
                interval: TimeSpan.FromMilliseconds(1)));

            Assert.That(exception, Is.SameAs(failure));
            Assert.That(spy.Lines.Single(line => line?.StartsWith("FAILURE > ", StringComparison.Ordinal) == true),
                Does.Contain(new string('x', 1024) + " [truncated]").And.Not.Contain(new string('x', 1025)));
        }

        [Test]
        public void EventuallyAsync_FormattingFailureDoesNotMaskTimeout()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var exception = Assert.ThrowsAsync<TimeoutException>(() => test.EventuallyAsync(
                _ => Task.FromResult(new UnprintableObservation()),
                _ => false,
                timeout: TimeSpan.FromMilliseconds(20)));

            Assert.That(exception!.Message, Does.Contain("observation formatting failed: InvalidOperationException"));
        }

        [Test]
        public void EventuallyAsync_DiagnosticOutputFailureDoesNotMaskActionFailure()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var implementor = new Mock<TestFrameworkImplementor>();
            implementor.Setup(value => value.WriteLine(It.IsAny<string>())).Throws(new InvalidOperationException("Output unavailable."));
            test.ReplaceTestFrameworkImplementor(implementor.Object);
            var failure = new InvalidOperationException("poll failed");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => test.EventuallyAsync(
                _ => Task.FromException<bool>(failure),
                value => value));

            Assert.That(exception, Is.SameAs(failure));
        }

        [Test]
        public void EventuallyAsync_ConditionFailureReportsCompletedNullObservation()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);
            var failure = new InvalidOperationException("condition failed");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => test.EventuallyAsync<string?>(
                _ => Task.FromResult<string?>(null),
                _ => throw failure));

            Assert.That(exception, Is.SameAs(failure));
            Assert.That(spy.Lines.Single(), Does.Contain("Attempts: 1. Last observation: <null>"));
        }
        [Test]
        public void EventuallyAsync_CancelsAnOperationThatExceedsTimeout()
        {
            using var test = FunctionTester.Create<HttpFunction>();

            var exception = Assert.ThrowsAsync<TimeoutException>(() => test.EventuallyAsync(
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    return false;
                },
                value => value,
                timeout: TimeSpan.FromMilliseconds(20)));

            Assert.That(exception!.Message, Does.Contain("Last observation: <no completed observation>"));
        }

        [Test]
        public void EventuallyAsync_PropagatesCallerCancellation()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => test.EventuallyAsync(
                _ => Task.FromResult(false),
                value => value,
                cancellationToken: cancellation.Token));
        }

        [Test]
        public void EventuallyAsync_TimesOutWhenOperationIgnoresCancellation()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = test.EventuallyAsync(
                _ => pending.Task,
                value => value,
                timeout: TimeSpan.FromMilliseconds(20));

            try
            {
                Assert.ThrowsAsync<TimeoutException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.That(operation.IsCompleted, Is.True, "The polling timeout must bound the operation itself.");
            }
            finally
            {
                pending.TrySetResult(false);
            }
        }

        [Test]
        public void EventuallyAsync_CancelsWaitWhenOperationIgnoresCancellation()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            using var cancellation = new CancellationTokenSource();
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = test.EventuallyAsync(
                _ => pending.Task,
                value => value,
                timeout: Timeout.InfiniteTimeSpan,
                cancellationToken: cancellation.Token);

            try
            {
                cancellation.Cancel();
                var exception = Assert.CatchAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
                Assert.That(operation.IsCanceled, Is.True);
            }
            finally
            {
                pending.TrySetResult(false);
            }
        }

        private sealed class UnprintableObservation
        {
            /// <summary>
            /// Simulates an observation that cannot be formatted for diagnostic output.
            /// </summary>
            public override string ToString() => throw new InvalidOperationException("Cannot format observation.");
        }
    }
}
