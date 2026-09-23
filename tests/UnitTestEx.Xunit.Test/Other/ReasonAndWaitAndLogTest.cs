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
    public class ReasonAndWaitAndLogTest : UnitTestBase
    {
        public ReasonAndWaitAndLogTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Reason_WritesReasonToOutput()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var result = test.Reason("Confirming Reason() writes context to the test output.");

            Assert.Same(test, result);
            Assert.Contains("REASON >", spy.Lines);
            Assert.Contains("Confirming Reason() writes context to the test output.", spy.Lines);
        }

        [Fact]
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

            Assert.Same(test, result);
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(100), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.Contains(spy.Lines, l => l != null && l.Contains("WAIT >") && l.Contains("Waiting for a background process to log."));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Timeout:"));
            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Background message logged during the wait window."));
        }

        [Fact]
        public void WaitAndLog_ExcludesLoggingThatOccurredBeforeTheWaitStarted()
        {
            using var test = ApiTester.Create<Startup>();
            var spy = new SpyTestFrameworkImplementor(test.Implementor);
            test.ReplaceTestFrameworkImplementor(spy);

            var logger = test.Services.GetRequiredService<ILogger<PersonController>>();
            logger.LogInformation("Stale message logged before the wait started.");

            test.WaitAndLog("Waiting with nothing new expected.", TimeSpan.FromMilliseconds(200));

            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains("None.", spy.Lines);
            Assert.DoesNotContain(spy.Lines, l => l != null && l.Contains("Stale message logged before the wait started."));
        }
    }
}
