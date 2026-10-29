using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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

            Assert.ThrowsAsync<TimeoutException>(() => test.EventuallyAsync(
                _ => Task.FromResult(false),
                value => value,
                timeout: TimeSpan.FromMilliseconds(20),
                interval: TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void EventuallyAsync_CancelsAnOperationThatExceedsTimeout()
        {
            using var test = FunctionTester.Create<HttpFunction>();

            Assert.ThrowsAsync<TimeoutException>(() => test.EventuallyAsync(
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    return false;
                },
                value => value,
                timeout: TimeSpan.FromMilliseconds(20)));
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
    }
}
