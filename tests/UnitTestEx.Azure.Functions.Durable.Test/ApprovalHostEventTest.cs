using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;
using UnitTestEx;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies external-event completion of the approval orchestration in the in-memory Durable Task host.
/// </summary>
[TestFixture]
public sealed class ApprovalHostEventTest
{
    /// <summary>Verifies an external event completes the orchestration without waiting for its 24-hour timer.</summary>
    [Test]
    public async Task ApprovalEventCompletesBeforeProductionLengthTimeout()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        DurableTaskTestHost? host = null;

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
                registry.AddOrchestratorFuncWithTimerDiagnostics<ApprovalRequest, ApprovalDecision>(
                    nameof(ApprovalOrchestration.AwaitApproval),
                    (context, _) => ApprovalOrchestration.AwaitApproval(context)));

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ApprovalOrchestration.AwaitApproval),
                new ApprovalRequest(TimeSpan.FromHours(24)));
            await host.Client.WaitForInstanceStartAsync(instanceId).WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.RaiseEventAsync(instanceId, "Approved", new ApprovalDecision(true));

            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<ApprovalDecision>(), Is.EqualTo(new ApprovalDecision(true)));
            Assert.That(functions.SharedState.GetLoggerMessages().Any(message =>
                message?.Contains("uses the host's real wall clock", StringComparison.Ordinal) == true), Is.True);

            await host.Client.RaiseEventAsync(instanceId, "Approved", new ApprovalDecision(false));
            var afterLateEvent = await host.Client.GetInstanceAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(afterLateEvent!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(afterLateEvent.ReadOutputAs<ApprovalDecision>(), Is.EqualTo(new ApprovalDecision(true)));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Verifies the real approval orchestration returns a timeout decision for an explicit short test input.</summary>
    [Test]
    public async Task ExplicitShortTestTimeoutCompletesWithDeniedDecision()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        DurableTaskTestHost? host = null;

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
                registry.AddOrchestratorFuncWithTimerDiagnostics<ApprovalRequest, ApprovalDecision>(
                    nameof(ApprovalOrchestration.AwaitApproval),
                    (context, _) => ApprovalOrchestration.AwaitApproval(context)));

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ApprovalOrchestration.AwaitApproval),
                new ApprovalRequest(TimeSpan.FromMilliseconds(100)));
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<ApprovalDecision>(), Is.EqualTo(new ApprovalDecision(false)));
            Assert.That(functions.SharedState.GetLoggerMessages().Any(message =>
                message?.Contains("uses the host's real wall clock", StringComparison.Ordinal) == true), Is.True);
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
