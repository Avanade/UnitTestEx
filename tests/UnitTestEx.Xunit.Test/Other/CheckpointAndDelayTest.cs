using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    public class CheckpointAndDelayTest : UnitTestBase
    {
        public CheckpointAndDelayTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Checkpoint_WritesReasonAndFlushesLogsToOutput()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var result = test.Checkpoint("Confirming Checkpoint() writes context to the test output.");

            Assert.Same(test, result);
            Assert.Contains("CHECKPOINT >", spy.Lines);
            Assert.Contains("Confirming Checkpoint() writes context to the test output.", spy.Lines);
            Assert.DoesNotContain("LOGGING >", spy.Lines);
        }

        [Fact]
        public void Checkpoint_SurfacesLoggingCapturedSinceTheLastCheckpointOrDelay()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Message logged before the checkpoint.");

            test.Checkpoint("Confirming Checkpoint() also flushes background logging, like Delay() does.");

            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Message logged before the checkpoint."));
        }

        [Fact]
        public async Task Delay_DelaysAndSurfacesLoggingThatOccursDuringTheDelay()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            // A logger resolved directly from the host's DI container (i.e. not via a UnitTestEx tester invocation) simulates background/hosted-service logging.
            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();

            var backgroundLogTask = Task.Run(async () =>
            {
                await Task.Delay(300);
                logger.LogInformation("Background message logged during the delay window.");
            });

            var sw = Stopwatch.StartNew();
            var result = test.Delay(TimeSpan.FromSeconds(2), "Waiting for a background process to log.");
            sw.Stop();

            await backgroundLogTask;

            Assert.Same(test, result);
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(100), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.Contains(spy.Lines, l => l != null && l.Contains("DELAY (00:00:02) >"));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Waiting for a background process to log."));
            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Background message logged during the delay window."));
        }

        [Fact]
        public void Delay_ExcludesLoggingThatOccurredBeforeTheDelayStarted()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Stale message logged before the delay started.");

            test.Delay(TimeSpan.FromMilliseconds(200), "Waiting with nothing new expected.");

            Assert.DoesNotContain("LOGGING >", spy.Lines);
            Assert.DoesNotContain("None.", spy.Lines);
            Assert.DoesNotContain(spy.Lines, l => l != null && l.Contains("Stale message logged before the delay started."));
        }
    }
}
