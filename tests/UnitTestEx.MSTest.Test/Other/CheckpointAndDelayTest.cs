using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class CheckpointAndDelayTest
    {
        [TestMethod]
        public void Checkpoint_WritesReasonAndFlushesLogsToOutput()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var result = test.Checkpoint("Confirming Checkpoint() writes context to the test output.");

            Assert.AreSame(test, result);
            Assert.IsTrue(spy.Lines.Contains("CHECKPOINT >"));
            Assert.IsTrue(spy.Lines.Contains("Confirming Checkpoint() writes context to the test output."));
            Assert.IsFalse(spy.Lines.Contains("LOGGING >"));
        }

        [TestMethod]
        public void Checkpoint_SurfacesLoggingCapturedSinceTheLastCheckpointOrDelay()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Message logged before the checkpoint.");

            test.Checkpoint("Confirming Checkpoint() also flushes background logging, like Delay() does.");

            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Message logged before the checkpoint.")));
        }

        [TestMethod]
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

            Assert.AreSame(test, result);
            Assert.IsTrue(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(100), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("DELAY (00:00:02) >")));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Waiting for a background process to log.")));
            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Background message logged during the delay window.")));
        }

        [TestMethod]
        public void Delay_ExcludesLoggingThatOccurredBeforeTheDelayStarted()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Stale message logged before the delay started.");

            test.Delay(TimeSpan.FromMilliseconds(200), "Waiting with nothing new expected.");

            Assert.IsFalse(spy.Lines.Contains("LOGGING >"));
            Assert.IsFalse(spy.Lines.Contains("None."));
            Assert.IsFalse(spy.Lines.Any(l => l != null && l.Contains("Stale message logged before the delay started.")));
        }
    }
}
