using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Aspire.Xunit.Test;

/// <summary>Verifies request-log capture when forwarded headers and messages arrive after capture starts.</summary>
/// <param name="output">The xUnit test output.</param>
public sealed class ResourceLogCaptureTest(ITestOutputHelper output) : UnitTestBase(output)
{
    /// <summary>Waits for a resource's first forwarded log instead of returning when its buffer does not yet exist.</summary>
    /// <param name="error">Whether the forwarded message should trigger the configured error check.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstForwardedEntryIsCapturedAfterReadStarts(bool error)
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>().WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");
        var app = await tester.GetDistributedApplicationAsync();
        tester.ResourceLogGracePeriod = TimeSpan.FromSeconds(2);
        tester.ResourceLogQuietPeriod = TimeSpan.FromMilliseconds(50);
        tester.ResourceLogMaxWait = TimeSpan.FromSeconds(5);
        var source = tester.Http("late-log").HttpClientSource;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string?[]? entries = null;
        Task<Exception?> read = Task.Run(() =>
        {
            started.SetResult();
            return Record.Exception(() => entries = source.GetRequestLogMessages("request")?.ToArray());
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        var factory = app.Services.GetRequiredService<ILoggerFactory>();
        var applicationName = app.Services.GetRequiredService<IHostEnvironment>().ApplicationName;
        var logger = factory.CreateLogger($"{applicationName}.Resources.late-log");
        WriteForwardedLine(logger, 0, $"{(error ? "fail" : "info")}: DelayedResource[0]");
        WriteForwardedLine(logger, 1, "      Delayed resource message.");
        var exception = await read.WaitAsync(TimeSpan.FromSeconds(5));
        if (error)
            Assert.Contains("Delayed resource message.", Assert.IsType<XunitException>(exception).Message);
        else
        {
            Assert.Null(exception);
            Assert.Contains(entries ?? [], entry => entry is not null && entry.Contains("Delayed resource message.", StringComparison.Ordinal));
        }
    }

    /// <summary>Retains a forwarded error header when a forced flush occurs before its message line arrives.</summary>
    [Fact]
    public async Task FlushingHeaderDoesNotLoseDelayedMessageSeverity()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>().WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
            .ErrorWhenLogContains(include: ["Delayed error message*"]);
        var app = await tester.GetDistributedApplicationAsync();
        var applicationName = app.Services.GetRequiredService<IHostEnvironment>().ApplicationName;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"{applicationName}.Resources.split-log");
        WriteForwardedLine(logger, 0, "fail: DelayedResource[0]");
        tester.Checkpoint("Flush before the error body arrives.");
        WriteForwardedLine(logger, 1, "      Delayed error message.");
        var exception = Assert.Throws<XunitException>(() => tester.Checkpoint("Flush the delayed error body."));
        Assert.Contains("Delayed error message.", exception.Message);
    }

    /// <summary>Writes one physical line in the same format as Aspire's console-log forwarding.</summary>
    /// <param name="logger">The AppHost resource-category logger.</param>
    /// <param name="index">The physical line index.</param>
    /// <param name="text">The forwarded header or message text.</param>
    private static void WriteForwardedLine(ILogger logger, int index, string text)
        => logger.LogInformation("{Line}", $"{index}: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)}Z {text}");
}
