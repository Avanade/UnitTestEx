using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies external events are buffered while an orchestration is suspended before its event wait.
/// </summary>
[TestFixture]
public sealed class EarlyExternalEventTest
{
    /// <summary>Verifies an event raised during a preceding activity is delivered after the event wait is registered.</summary>
    [Test]
    public async Task EventRaisedBeforeWaitIsBufferedAndDeserialized()
    {
        DurableTaskTestHost? host = null;
        var gateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, ApprovalDecision>(
                    "EarlyEventOrchestration",
                    async (context, input) =>
                    {
                        await context.CallActivityAsync<string>("EarlyEventGate", input);
                        return await context.WaitForExternalEvent<ApprovalDecision>("Approved");
                    });
                registry.AddActivityFunc<string, string>(
                    "EarlyEventGate",
                    async (_, input) =>
                    {
                        gateStarted.TrySetResult();
                        await releaseGate.Task;
                        return input;
                    });
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("EarlyEventOrchestration", "hold");
            await gateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var decision = new ApprovalDecision(true);
            await host.Client.RaiseEventAsync(instanceId, "Approved", decision);
            releaseGate.TrySetResult();

            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<ApprovalDecision>(), Is.EqualTo(decision));
        }
        finally
        {
            releaseGate.TrySetResult();
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Verifies multiple same-name events raised before either wait are delivered in arrival order.</summary>
    [Test]
    public async Task MultipleEventsRaisedBeforeWaitsAreBufferedInArrivalOrder()
    {
        DurableTaskTestHost? host = null;
        var gateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "MultipleEarlyEventOrchestration",
                    async (context, input) =>
                    {
                        await context.CallActivityAsync<string>("MultipleEarlyEventGate", input);
                        string first = await context.WaitForExternalEvent<string>("Progress");
                        string second = await context.WaitForExternalEvent<string>("Progress");
                        return $"{first}|{second}";
                    });
                registry.AddActivityFunc<string, string>(
                    "MultipleEarlyEventGate",
                    async (_, input) =>
                    {
                        gateStarted.TrySetResult();
                        await releaseGate.Task;
                        return input;
                    });
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("MultipleEarlyEventOrchestration", "hold");
            await gateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.RaiseEventAsync(instanceId, "Progress", "first");
            await host.Client.RaiseEventAsync(instanceId, "Progress", "second");
            releaseGate.TrySetResult();

            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<string>(), Is.EqualTo("first|second"));
        }
        finally
        {
            releaseGate.TrySetResult();
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
