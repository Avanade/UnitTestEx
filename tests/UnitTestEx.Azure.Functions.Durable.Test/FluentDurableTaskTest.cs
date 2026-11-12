using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies the fluent API runs orchestration bodies through the in-memory SDK host.
/// </summary>
[TestFixture]
public sealed class FluentDurableTaskTest
{
    /// <summary>Starts a no-input orchestration and asserts its result.</summary>
    [Test]
    public async Task StartsNoInputOrchestration()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string>(nameof(CompleteWithoutInput), CompleteWithoutInput)
            .StartAsync();

        run.ExpectSuccess();
        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        result.AssertCompleted().AssertOutput("Completed without input");
    }

    /// <summary>Runs a real orchestration method and asserts its recovered result.</summary>
    [Test]
    public async Task ExecutesFunctionMethodAndAssertsOutput()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>(nameof(FailureOrchestration.RecoverWorkflow), (context, _) => FailureOrchestration.RecoverWorkflow(context))
            .Activity<string, string>(nameof(ActivityTransforms.RejectWorkflow), (_, reference) => ActivityTransforms.RejectWorkflow(reference))
            .StartAsync("ORDER-FAIL");

        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        result.AssertCompleted().AssertOutput("Declined");
        Assert.That(result.Metadata.ReadCustomStatusAs<string>(), Is.EqualTo("Declined"));
    }

    /// <summary>Exposes SDK failure details through the fluent result assertor.</summary>
    [Test]
    public async Task ExposesFailureCauseThroughFluentAssertor()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>(nameof(FailureOrchestration.DeclineWorkflow), (context, _) => FailureOrchestration.DeclineWorkflow(context))
            .Activity<string, string>(nameof(ActivityTransforms.RejectWorkflow), (_, reference) => ActivityTransforms.RejectWorkflow(reference))
            .StartAsync("ORDER-FAIL");

        run.ExpectException().Type<DurableTaskOrchestrationException>("Workflow ORDER-FAIL was declined.");

        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));

        result.AssertFailed().AssertFailureCausedBy<WorkflowRejectedException>();
        Assert.That(result.FailureDetails?.InnerFailure?.ErrorMessage, Is.EqualTo("Workflow ORDER-FAIL was declined."));
    }

    /// <summary>Warns that a long host timer uses wall-clock time while allowing an event to complete the orchestration.</summary>
    [Test]
    public async Task ApprovalEventCompletesBeforeLongTimerAndLogsDiagnostic()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<ApprovalRequest, ApprovalDecision>(nameof(ApprovalOrchestration.AwaitApproval), (context, _) => ApprovalOrchestration.AwaitApproval(context))
            .StartAsync(new ApprovalRequest(TimeSpan.FromHours(24)));

        run.ExpectSuccess().ExpectLogContains("uses the host's real wall clock");

        await run.Host.Client.WaitForInstanceStartAsync(run.InstanceId).WaitAsync(TimeSpan.FromSeconds(5));
        await run.Host.Client.RaiseEventAsync(run.InstanceId, "Approved", new ApprovalDecision(true));

        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        result.AssertCompleted().AssertOutput(new ApprovalDecision(true));
    }

    /// <summary>Completes without reading orchestration input.</summary>
    /// <param name="context">The orchestration context.</param>
    /// <returns>The completion result.</returns>
    private static Task<string> CompleteWithoutInput(TaskOrchestrationContext context) => Task.FromResult("Completed without input");
}
