using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Aspire.NUnit.Test;

[TestFixture]
public sealed class FailureDiagnosticsTest
{
    [TestCase("failure")]
    [TestCase("timeout")]
    [TestCase("cancellation")]
    public async Task FailedSetup_ReportsBoundedLogsAndStates_AndCanReset(string kind)
    {
        Exception failure = kind switch
        {
            "timeout" => new TimeoutException("startup timed out"),
            "cancellation" => new OperationCanceledException(new CancellationToken(true)),
            _ => new InvalidOperationException("setup failed")
        };
        var calls = 0;
        var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
            .BeforeStart(app =>
            {
                calls++;
                var name = app.Services.GetRequiredService<IHostEnvironment>().ApplicationName;
                var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"{name}.Resources.api");
                for (var i = 0; i < 60; i++)
                    logger.LogInformation("diagnostic-entry-{Index:D2}", i);
                logger.LogInformation("{LongEntry}", new string('x', 5000));
                throw failure;
            });
        var spy = new SpyTestFrameworkImplementor(tester.Implementor);
        tester.ReplaceTestFrameworkImplementor(spy);

        try
        {
            var exception = Assert.CatchAsync<Exception>(() => tester.GetDistributedApplicationAsync());
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(spy.Lines, Does.Contain("RESOURCE STATES >"));
            Assert.That(spy.Lines.Any(line => line?.StartsWith("api:", StringComparison.Ordinal) == true), Is.True);
            Assert.That(spy.Lines, Does.Contain("diagnostic-entry-59"));
            Assert.That(spy.Lines, Does.Not.Contain("diagnostic-entry-10"));
            Assert.That(spy.Lines.Count(line => line?.StartsWith("diagnostic-entry-", StringComparison.Ordinal) == true), Is.EqualTo(49));
            Assert.That(spy.Lines, Does.Contain(new string('x', 4096) + " [truncated]"));

            Assert.DoesNotThrow(tester.ResetHost);
            Assert.CatchAsync<Exception>(() => tester.GetDistributedApplicationAsync());
            Assert.That(calls, Is.EqualTo(2));
        }
        finally
        {
            await tester.DisposeAsync();
            await tester.DisposeAsync();
        }

        Assert.Throws<ObjectDisposedException>(() => tester.GetDistributedApplicationAsync());
    }

    [Test]
    public async Task PollingFailure_DoesNotStartAnApplication()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
            .BeforeStart(_ => throw new InvalidOperationException("Must not start."));
        var spy = new SpyTestFrameworkImplementor(tester.Implementor);
        tester.ReplaceTestFrameworkImplementor(spy);

        Assert.ThrowsAsync<TimeoutException>(() => tester.EventuallyAsync(
            _ => Task.FromResult("output missing"),
            _ => false,
            timeout: TimeSpan.FromMilliseconds(20)));

        Assert.That(spy.Lines, Does.Contain("No built application is available."));
        Assert.That(spy.Lines.Any(line => line?.Contains("Last observation: output missing", StringComparison.Ordinal) == true), Is.True);
    }
}
