using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Observes activity behavior when its parent orchestration is terminated.
/// </summary>
[TestFixture]
public sealed class ActivityTerminationTest
{
    /// <summary>Verifies a running activity delegate can finish after its parent is terminated.</summary>
    [Test]
    public async Task RunningActivityCompletesAfterParentTermination()
    {
        DurableTaskTestHost? host = null;
        var activityStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActivity = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activityCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "ParentWaitingOnActivity",
                    (context, input) => context.CallActivityAsync<string>("BlockingActivity", input));
                registry.AddActivityFunc<string, string>(
                    "BlockingActivity",
                    async (_, input) =>
                    {
                        activityStarted.TrySetResult();
                        await releaseActivity.Task;
                        activityCompleted.TrySetResult();
                        return input;
                    });
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ParentWaitingOnActivity", "activity-result");
            await activityStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.TerminateInstanceAsync(instanceId);

            var terminatedParent = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(terminatedParent!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));

            releaseActivity.TrySetResult();
            await activityCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var parentAfterActivityCompletion = await host.Client.GetInstanceAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(parentAfterActivityCompletion!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));
        }
        finally
        {
            releaseActivity.TrySetResult();
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
