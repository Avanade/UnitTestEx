using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;
using System.Text.Json;

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
        DurableTaskTestHost? host = null;

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
                registry.AddOrchestratorFunc<string, string>(
                    "CustomStatusProgression",
                    async (context, input) =>
                    {
                        context.SetCustomStatus(new { Stage = "Started", Version = 1 });
                        await context.WaitForExternalEvent<string>("Advance");
                        context.SetCustomStatus(new { Stage = "Waiting", Version = 2 });
                        await context.WaitForExternalEvent<string>("Finish");
                        context.SetCustomStatus(new { Stage = "Completed", Version = 3 });
                        return input;
                    }));

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("CustomStatusProgression", "done");
            await host.Client.WaitForInstanceStartAsync(instanceId).WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForCustomStatusAsync(host.Client, instanceId, "Started");

            await host.Client.RaiseEventAsync(instanceId, "Advance", "advance");
            await WaitForCustomStatusAsync(host.Client, instanceId, "Waiting");

            await host.Client.RaiseEventAsync(instanceId, "Finish", "finish");
            var completed = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(completed!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completed.ReadOutputAs<string>(), Is.EqualTo("done"));
            AssertCustomStatus(completed.ReadCustomStatusAs<JsonElement>(), "Completed", 3);
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Verifies assigning null clears custom status from running and completed metadata.</summary>
    [Test]
    public async Task NullCustomStatusClearsRunningAndTerminalMetadata()
    {
        DurableTaskTestHost? host = null;

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
                registry.AddOrchestratorFunc<string, string>(
                    "CustomStatusClear",
                    async (context, input) =>
                    {
                        context.SetCustomStatus(new { Stage = "Started", Version = 1 });
                        await context.WaitForExternalEvent<string>("Clear");
                        context.SetCustomStatus(null);
                        await context.WaitForExternalEvent<string>("Finish");
                        return input;
                    }));

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("CustomStatusClear", "done");
            await host.Client.WaitForInstanceStartAsync(instanceId).WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForCustomStatusAsync(host.Client, instanceId, "Started");

            await host.Client.RaiseEventAsync(instanceId, "Clear", "clear");
            var clearedMetadata = await WaitForCustomStatusClearedAsync(host.Client, instanceId);
            Assert.That(clearedMetadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));
            Assert.That(clearedMetadata.SerializedCustomStatus, Is.Null);

            await host.Client.RaiseEventAsync(instanceId, "Finish", "finish");
            var completed = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(completed!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completed.SerializedCustomStatus, Is.Null);
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Waits until the requested status is visible from the test-host client.</summary>
    /// <param name="client">The test-host client.</param>
    /// <param name="instanceId">The orchestration instance ID.</param>
    /// <param name="stage">The expected stage name.</param>
    /// <returns>A task that completes when the status is observed.</returns>
    private static async Task WaitForCustomStatusAsync(DurableTaskClient client, string instanceId, string stage)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
            if (metadata is not null)
            {
                JsonElement status = metadata.ReadCustomStatusAs<JsonElement>();
                if (status.ValueKind == JsonValueKind.Object
                    && status.TryGetProperty("Stage", out JsonElement actualStage)
                    && actualStage.GetString() == stage)
                {
                    int expectedVersion = stage switch
                    {
                        "Started" => 1,
                        "Waiting" => 2,
                        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
                    };
                    AssertCustomStatus(status, stage, expectedVersion);
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new TimeoutException($"Custom status '{stage}' was not observed for orchestration '{instanceId}'.");
    }

    /// <summary>Waits until the orchestration metadata no longer contains serialized custom status.</summary>
    /// <param name="client">The test-host client.</param>
    /// <param name="instanceId">The orchestration instance ID.</param>
    /// <returns>The metadata snapshot with custom status cleared.</returns>
    private static async Task<OrchestrationMetadata> WaitForCustomStatusClearedAsync(DurableTaskClient client, string instanceId)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
            if (metadata is not null && metadata.SerializedCustomStatus is null)
                return metadata;

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new TimeoutException($"Custom status was not cleared for orchestration '{instanceId}'.");
    }

    /// <summary>Asserts the stage and version in a custom-status payload.</summary>
    /// <param name="status">The status payload.</param>
    /// <param name="stage">The expected stage.</param>
    /// <param name="version">The expected version.</param>
    private static void AssertCustomStatus(JsonElement status, string stage, int version)
    {
        Assert.That(status.GetProperty("Stage").GetString(), Is.EqualTo(stage));
        Assert.That(status.GetProperty("Version").GetInt32(), Is.EqualTo(version));
    }
}
