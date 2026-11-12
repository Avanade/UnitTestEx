using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies client termination of a pending orchestration in the in-memory Durable Task host.
/// </summary>
[TestFixture]
public sealed class OrchestrationTerminationTest
{
    /// <summary>Verifies terminating an orchestration waiting for an external event yields terminal metadata.</summary>
    [Test]
    public async Task PendingOrchestrationCanBeTerminated()
    {
        DurableTaskTestHost? host = null;
        var waitRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
                registry.AddOrchestratorFunc<string, string>(
                    "TerminableExternalEventOrchestration",
                    async (context, _) =>
                    {
                        Task<string> wait = context.WaitForExternalEvent<string>("NeverRaised");
                        waitRegistered.TrySetResult();
                        return await wait;
                    }));

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("TerminableExternalEventOrchestration", "pending");
            await waitRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.TerminateInstanceAsync(instanceId);

            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Verifies a pending child remains independent when its parent is terminated.</summary>
    [Test]
    public async Task TerminatingParentLeavesPendingChildRunningUntilSeparatelyTerminated()
    {
        DurableTaskTestHost? host = null;
        var childWaitRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failureChildWaitRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string childInstanceId = "pending-independent-child";
        const string failureChildInstanceId = "pending-independent-failure-child";

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "ParentWaitingOnChild",
                    (context, input) => context.CallSubOrchestratorAsync<string>(
                        "ChildWaitingOnEvent",
                        input,
                        new SubOrchestrationOptions(instanceId: input == "failure" ? failureChildInstanceId : childInstanceId)));
                registry.AddOrchestratorFunc<string, string>(
                    "ChildWaitingOnEvent",
                    async (context, input) =>
                    {
                        Task<string> wait = context.WaitForExternalEvent<string>("Continue");
                        if (input == "failure")
                            failureChildWaitRegistered.TrySetResult();
                        else
                            childWaitRegistered.TrySetResult();

                        string result = await wait;
                        if (input == "failure")
                            throw new InvalidOperationException("The child failed after its parent was terminated.");

                        return result;
                    });
            });

            var parentId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ParentWaitingOnChild", "pending");
            await childWaitRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.TerminateInstanceAsync(parentId);

            var parentMetadata = await host.Client.WaitForInstanceCompletionAsync(parentId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var childMetadata = await host.Client.GetInstanceAsync(childInstanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(parentMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));
            Assert.That(childMetadata, Is.Not.Null);
            Assert.That(childMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));

            await host.Client.RaiseEventAsync(childInstanceId, "Continue", "completed independently");
            var completedChild = await host.Client.WaitForInstanceCompletionAsync(childInstanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var parentAfterChildCompletion = await host.Client.GetInstanceAsync(parentId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(completedChild!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completedChild.ReadOutputAs<string>(), Is.EqualTo("completed independently"));
            Assert.That(parentAfterChildCompletion!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));

            var failedParentId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ParentWaitingOnChild", "failure");
            await failureChildWaitRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.TerminateInstanceAsync(failedParentId);

            var failedParentMetadata = await host.Client.WaitForInstanceCompletionAsync(failedParentId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(failedParentMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));

            await host.Client.RaiseEventAsync(failureChildInstanceId, "Continue", "fail");
            var failedChildMetadata = await host.Client.WaitForInstanceCompletionAsync(failureChildInstanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var parentAfterChildFailure = await host.Client.GetInstanceAsync(failedParentId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(failedChildMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            Assert.That(failedChildMetadata.FailureDetails?.IsCausedBy<InvalidOperationException>(), Is.True);
            Assert.That(parentAfterChildFailure!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Terminated));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
