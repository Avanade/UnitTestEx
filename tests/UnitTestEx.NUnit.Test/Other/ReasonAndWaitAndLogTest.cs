using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ReasonAndWaitAndLogTest
    {
        [Test]
        public void Reason_WritesReasonToOutput()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var result = test.Reason("Confirming Reason() writes context to the test output.");

            Assert.That(result, Is.SameAs(test));
            Assert.That(spy.Lines, Does.Contain("REASON >"));
            Assert.That(spy.Lines, Does.Contain("Confirming Reason() writes context to the test output."));
        }

        [Test]
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

            Assert.That(result, Is.SameAs(test));
            Assert.That(sw.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(100)), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("WAIT >") && l.Contains("Waiting for a background process to log.")), Is.True);
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("Timeout:")), Is.True);
            Assert.That(spy.Lines, Does.Contain("LOGGING >"));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("Background message logged during the wait window.")), Is.True);
        }

        [Test]
        public void WaitAndLog_ExcludesLoggingThatOccurredBeforeTheWaitStarted()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Stale message logged before the wait started.");

            test.WaitAndLog("Waiting with nothing new expected.", TimeSpan.FromMilliseconds(200));

            Assert.That(spy.Lines, Does.Contain("LOGGING >"));
            Assert.That(spy.Lines, Does.Contain("None."));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("Stale message logged before the wait started.")), Is.False);
        }
    }
}
