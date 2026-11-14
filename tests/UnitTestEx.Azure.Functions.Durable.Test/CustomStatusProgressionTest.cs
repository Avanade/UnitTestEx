using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using NUnit.Framework;
using System.Text.Json;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies successive custom-status updates in a running orchestration.
/// </summary>
[TestFixture]
public sealed class CustomStatusProgressionTest
{
    /// <summary>Verifies the latest status is visible between durable waits and remains in terminal metadata.</summary>
    [Test]
    public async Task LatestCustomStatusIsVisibleDuringAndAfterExecution()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>("CustomStatusProgression",
                async (context, input) =>
                {
                    context.SetCustomStatus(new { Stage = "Started", Version = 1 });
                    await context.WaitForExternalEvent<string>("Advance");

                    context.SetCustomStatus(new { Stage = "Waiting", Version = 2 });
                    await context.WaitForExternalEvent<string>("Finish");

                    context.SetCustomStatus(new { Stage = "Completed", Version = 3 });
                    return input;
                })
            .StartAsync("done");

        var started = await run.WaitForCustomStatusAsync(metadata => HasCustomStatus(metadata, "Started", 1));
        AssertCustomStatus(started.ReadCustomStatusAs<JsonElement>(), "Started", 1);
        Assert.ThrowsAsync<TimeoutException>(async () =>
            await run.WaitForCustomStatusAsync(_ => false, TimeSpan.FromMilliseconds(50)));

        await run.RaiseEventAsync("Advance", "advance");
        var waiting = await run.WaitForCustomStatusAsync(metadata => HasCustomStatus(metadata, "Waiting", 2));
        AssertCustomStatus(waiting.ReadCustomStatusAs<JsonElement>(), "Waiting", 2);

        await run.RaiseEventAsync("Finish", "finish");
        var completed = await run.WaitForCompletionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(completed.Metadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completed.Metadata.ReadOutputAs<string>(), Is.EqualTo("done"));
        });

        AssertCustomStatus(completed.Metadata.ReadCustomStatusAs<JsonElement>(), "Completed", 3);
    }

    /// <summary>Verifies assigning null clears custom status from running and completed metadata.</summary>
    [Test]
    public async Task NullCustomStatusClearsRunningAndTerminalMetadata()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>(
                "CustomStatusClear",
                async (context, input) =>
                {
                    context.SetCustomStatus(new { Stage = "Started", Version = 1 });
                    await context.WaitForExternalEvent<string>("Clear");

                    context.SetCustomStatus(null);
                    await context.WaitForExternalEvent<string>("Finish");

                    return input;
                })
            .StartAsync("done");

        var started = await run.WaitForCustomStatusAsync(metadata => HasCustomStatus(metadata, "Started", 1));
        AssertCustomStatus(started.ReadCustomStatusAs<JsonElement>(), "Started", 1);

        await run.RaiseEventAsync("Clear", "clear");
        var clearedMetadata = await run.WaitForCustomStatusAsync(metadata => metadata.SerializedCustomStatus is null);
        Assert.Multiple(() =>
        {
            Assert.That(clearedMetadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));
            Assert.That(clearedMetadata.SerializedCustomStatus, Is.Null);
        });

        await run.RaiseEventAsync("Finish", "finish");
        var completed = await run.WaitForCompletionAsync();
        Assert.Multiple(() =>
        {
            Assert.That(completed.Metadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completed.Metadata.SerializedCustomStatus, Is.Null);
        });
    }

    /// <summary>Checks whether the metadata contains the requested custom-status stage and version.</summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <param name="stage">The expected stage name.</param>
    /// <param name="version">The expected status version.</param>
    /// <returns>True when both values match.</returns>
    private static bool HasCustomStatus(OrchestrationMetadata metadata, string stage, int version)
    {
        if (metadata.SerializedCustomStatus is null)
            return false;

        JsonElement status = metadata.ReadCustomStatusAs<JsonElement>();
        return status.ValueKind == JsonValueKind.Object
            && status.TryGetProperty("Stage", out JsonElement actualStage)
            && actualStage.GetString() == stage
            && status.TryGetProperty("Version", out JsonElement actualVersion)
            && actualVersion.GetInt32() == version;
    }

    /// <summary>Asserts the stage and version in a custom-status payload.</summary>
    /// <param name="status">The status payload.</param>
    /// <param name="stage">The expected stage name.</param>
    /// <param name="version">The expected status version.</param>
    private static void AssertCustomStatus(JsonElement status, string stage, int version)
    {
        Assert.That(status.GetProperty("Stage").GetString(), Is.EqualTo(stage));
        Assert.That(status.GetProperty("Version").GetInt32(), Is.EqualTo(version));
    }
}
