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
    public class ReasonAndWaitAndLogTest
    {
        [TestMethod]
        public void Reason_WritesReasonToOutput()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var result = test.Reason("Confirming Reason() writes context to the test output.");

            Assert.AreSame(test, result);
            Assert.IsTrue(spy.Lines.Contains("REASON >"));
            Assert.IsTrue(spy.Lines.Contains("Confirming Reason() writes context to the test output."));
        }

        [TestMethod]
        public async Task WaitAndLog_WaitsAndSurfacesLoggingThatOccursDuringTheWait()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            // A logger resolved directly from the host's DI container (i.e. not via a UnitTestEx tester invocation) simulates background/hosted-service logging.
            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();

            var backgroundLogTask = Task.Run(async () =>
            {
                await Task.Delay(300);
                logger.LogInformation("Background message logged during the wait window.");
            });

            var sw = Stopwatch.StartNew();
            var result = test.WaitAndLog("Waiting for a background process to log.", TimeSpan.FromSeconds(2));
            sw.Stop();

            await backgroundLogTask;

            Assert.AreSame(test, result);
            Assert.IsTrue(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(100), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("WAIT >") && l.Contains("Waiting for a background process to log.")));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Timeout:")));
            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Background message logged during the wait window.")));
        }

        [TestMethod]
        public void WaitAndLog_ExcludesLoggingThatOccurredBeforeTheWaitStarted()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Stale message logged before the wait started.");

            test.WaitAndLog("Waiting with nothing new expected.", TimeSpan.FromMilliseconds(200));

            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Contains("None."));
            Assert.IsFalse(spy.Lines.Any(l => l != null && l.Contains("Stale message logged before the wait started.")));
        }
    }
}
