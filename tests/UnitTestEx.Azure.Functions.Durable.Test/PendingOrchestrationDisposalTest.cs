using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies bounded disposal of the in-memory Durable Task host with a pending orchestration.
/// </summary>
[TestFixture]
public sealed class PendingOrchestrationDisposalTest
{
    /// <summary>Verifies the host disposes while an orchestration waits indefinitely for an external event.</summary>
    [Test]
    public async Task HostDisposesWhileOrchestrationWaitsForExternalEvent()
    {
        DurableTaskTestHost? host = null;
        var waitRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
                registry.AddOrchestratorFunc<string, string>(
                    "PendingExternalEventOrchestration",
                    async (context, _) =>
                    {
                        Task<string> wait = context.WaitForExternalEvent<string>("NeverRaised");
                        waitRegistered.TrySetResult();
                        return await wait;
                    }));

            await host.Client.ScheduleNewOrchestrationInstanceAsync("PendingExternalEventOrchestration", "pending");
            await waitRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            host = null;
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Verifies host disposal completes while an activity delegate is blocked.</summary>
    [Test]
    public async Task HostDisposesWhileActivityDelegateIsRunning()
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
                    "PendingActivityOrchestration",
                    (context, input) => context.CallActivityAsync<string>("PendingActivity", input));
                registry.AddActivityFunc<string, string>(
                    "PendingActivity",
                    async (_, input) =>
                    {
                        activityStarted.TrySetResult();
                        await releaseActivity.Task;
                        activityCompleted.TrySetResult();
                        return input;
                    });
            });

            await host.Client.ScheduleNewOrchestrationInstanceAsync("PendingActivityOrchestration", "pending");
            await activityStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Task disposal = host.DisposeAsync().AsTask();
            try
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                releaseActivity.TrySetResult();
                await disposal.WaitAsync(TimeSpan.FromSeconds(5));
                host = null;
                throw;
            }

            host = null;
            Assert.That(activityCompleted.Task.IsCompleted, Is.False, "The registered delegate should still be blocked when host disposal returns.");
            releaseActivity.TrySetResult();
            await activityCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            releaseActivity.TrySetResult();
            if (host is not null)
                await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
