using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Concurrent;
using UnitTestEx.Abstractions;
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
    /// <summary>Writes managed output before assertions and only once for repeated completion waits.</summary>
    [Test]
    public async Task WritesManagedOutputBeforeAssertionsOnce()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var output = new CapturedOutput(functions.Implementor);
        functions.ReplaceTestFrameworkImplementor(output);
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>("OutputProbe", (context, input) =>
            {
                context.CreateReplaySafeLogger("OutputProbe").LogInformation("Application diagnostic");
                context.SetCustomStatus("finished");
                return Task.FromResult(input);
            })
            .StartAsync("hello");

        await run.WaitForCompletionAsync(assert: result =>
        {
            Assert.That(output.Text, Does.Contain("=== BEGIN DURABLE ORCHESTRATION TESTER ===")
                .And.Contain("Orchestration: OutputProbe").And.Contain(run.InstanceId)
                .And.Contain("INPUT >").And.Contain("\"hello\"")
                .And.Contain("LOGGING >").And.Contain("Application diagnostic")
                .And.Contain("Elapsed (ms):").And.Contain("Status: Completed")
                .And.Contain("Custom status: \"finished\"").And.Contain("Output: \"hello\""));
            result.AssertOutput("hello");
            Assert.That(output.Lines.SkipWhile(line => line != "=== BEGIN DURABLE ORCHESTRATION TESTER ===").Take(2),
                Is.EqualTo(new[] { "=== BEGIN DURABLE ORCHESTRATION TESTER ===", "" }));
            Assert.That(output.Lines.TakeLast(3), Is.EqualTo(new[] { "", "=== END DURABLE ORCHESTRATION TESTER ===", "" }));
        });
        await run.WaitForCompletionAsync();
        Assert.That(output.Lines.Count(line => line == "=== BEGIN DURABLE ORCHESTRATION TESTER ==="), Is.EqualTo(1));
        Assert.That(output.Lines.Count(line => line == "=== END DURABLE ORCHESTRATION TESTER ==="), Is.EqualTo(1));
    }

    /// <summary>Reports a running snapshot on timeout without preventing later completion diagnostics.</summary>
    [Test]
    public async Task TimeoutOutputDoesNotClaimOrchestrationFailed()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var output = new CapturedOutput(functions.Implementor);
        functions.ReplaceTestFrameworkImplementor(output);
        await using var run = await functions.DurableTask()
            .Orchestrator<string>("PendingOutputProbe", context =>
            {
                context.CreateReplaySafeLogger("PendingOutputProbe").LogInformation("Waiting for Finish");
                return context.WaitForExternalEvent<string>("Finish");
            })
            .StartAsync();

        run.ExpectSuccess().ExpectLogContains("Waiting for Finish");
        await run.WaitForStartAsync();
        Assert.ThrowsAsync<TimeoutException>(async () =>
            await run.WaitForCompletionAsync(TimeSpan.FromMilliseconds(50)));
        Assert.That(output.Text, Does.Contain("Completion wait timed out")
            .And.Contain("Last observed status: Running")
            .And.Contain("not an orchestration failure"));
        Assert.That(output.Text, Does.Not.Contain("Status: Failed"));
        Assert.That(output.Lines.TakeLast(3), Is.EqualTo(new[] { "", "=== END DURABLE ORCHESTRATION TESTER ===", "" }));
        await run.RaiseEventAsync("Finish", "done");
        await run.WaitForCompletionAsync(assert: result => result.AssertOutput("done"));
        Assert.That(output.Text, Does.Contain("Status: Completed"));
        Assert.That(output.Lines.Count(line => line == "=== BEGIN DURABLE ORCHESTRATION TESTER ==="), Is.EqualTo(2));
        Assert.That(output.Lines.Count(line => line == "=== END DURABLE ORCHESTRATION TESTER ==="), Is.EqualTo(2));
    }

    /// <summary>Starts a no-input orchestration and asserts its result.</summary>
    [Test]
    public async Task StartsNoInputOrchestration()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string>(nameof(CompleteWithoutInput), CompleteWithoutInput)
            .StartAsync();

        run.ExpectSuccess();
        DurableTaskTestAssertor<string>? callbackResult = null;
        var result = await run.WaitForCompletionAsync(assert: r =>
        {
            callbackResult = r;
            r.AssertCompleted().AssertOutput("Completed without input");
        });
        Assert.That(result, Is.SameAs(callbackResult));
    }

    /// <summary>Propagates exceptions thrown by the completion assertion callback.</summary>
    [Test]
    public async Task CompletionAssertionCallbackPropagatesExceptions()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var output = new CapturedOutput(functions.Implementor);
        functions.ReplaceTestFrameworkImplementor(output);
        await using var run = await functions.DurableTask()
            .Orchestrator<string>(nameof(CompleteWithoutInput), CompleteWithoutInput)
            .StartAsync();

        InvalidOperationException error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await run.WaitForCompletionAsync(assert: _ => throw new InvalidOperationException("Assertion callback failed")))!;
        Assert.That(error.Message, Is.EqualTo("Assertion callback failed"));
        Assert.That(output.Text, Does.Contain("Status: Completed").And.Contain("Output: \"Completed without input\""));
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
        var output = new CapturedOutput(functions.Implementor);
        functions.ReplaceTestFrameworkImplementor(output);
        await using var run = await functions.DurableTask()
            .Orchestrator<string, string>(nameof(FailureOrchestration.DeclineWorkflow), (context, _) => FailureOrchestration.DeclineWorkflow(context))
            .Activity<string, string>(nameof(ActivityTransforms.RejectWorkflow), (_, reference) => ActivityTransforms.RejectWorkflow(reference))
            .StartAsync("ORDER-FAIL");

        run.ExpectException().Type<DurableTaskOrchestrationException>("Workflow ORDER-FAIL was declined.");

        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));

        result.AssertFailed().AssertFailureCausedBy<WorkflowRejectedException>();
        Assert.That(result.FailureDetails?.InnerFailure?.ErrorMessage, Is.EqualTo("Workflow ORDER-FAIL was declined."));
        Assert.That(output.Text, Does.Contain("Status: Failed")
            .And.Contain("Failure:").And.Contain(nameof(WorkflowRejectedException))
            .And.Contain("Workflow ORDER-FAIL was declined."));
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

        await run.WaitForStartAsync();
        await run.RaiseEventAsync("Approved", new ApprovalDecision(true));

        var result = await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        result.AssertCompleted().AssertOutput(new ApprovalDecision(true));
    }

    /// <summary>Runs a typed instance with application DI, managed diagnostics and an unchanged long timer.</summary>
    [Test]
    public async Task TypedOrchestratorUsesApplicationDiAndManagedDiagnostics()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var scope = functions.Services.CreateScope();
        var orchestrator = ActivatorUtilities.CreateInstance<LoggedTypedOrchestrator>(scope.ServiceProvider);
        var output = new CapturedOutput(functions.Implementor);
        functions.ReplaceTestFrameworkImplementor(output);
        await using var run = await functions.DurableTask()
            .Orchestrator("TypedWithDi", orchestrator)
            .Activity<string, string>("ClassBasedEchoActivity", (_, input) => $"processed:{input}", configure: options => options.Times(Times.Once()))
            .StartAsync("hello");

        run.ExpectSuccess().ExpectLogContains("Typed orchestration input: hello").ExpectLogContains("uses the host's real wall clock");
        await run.WaitForCustomStatusAsync(metadata => metadata.ReadCustomStatusAs<string>() == "waiting");
        await run.RaiseEventAsync("Finish", "done");
        await run.WaitForCompletionAsync(assert: result =>
        {
            result.AssertCompleted().AssertOutput("processed:hello:done");
            Assert.That(output.Text, Does.Contain("Orchestration: TypedWithDi").And.Contain("Status: Completed").And.Contain("=== END DURABLE ORCHESTRATION TESTER ==="));
        });
        run.Verify();
    }

    /// <summary>Completes without reading orchestration input.</summary>
    /// <param name="context">The orchestration context.</param>
    /// <returns>The completion result.</returns>
    private static Task<string> CompleteWithoutInput(TaskOrchestrationContext context) => Task.FromResult("Completed without input");

    /// <summary>Uses an application logger and races a real host timer against an external event.</summary>
    /// <param name="logger">The logger resolved from application services.</param>
    private sealed class LoggedTypedOrchestrator(ILogger<LoggedTypedOrchestrator> logger) : TaskOrchestrator<string, string>
    {
        /// <summary>Calls an activity and waits for an event without shortening the timer.</summary>
        /// <param name="context">The SDK orchestration context.</param>
        /// <param name="input">The activity input.</param>
        /// <returns>The activity result combined with the event payload, or the timeout result.</returns>
        public override async Task<string> RunAsync(TaskOrchestrationContext context, string input)
        {
            if (!context.IsReplaying)
                logger.LogInformation("Typed orchestration input: {Input}", input);

            string result = await context.CallActivityAsync<string>("ClassBasedEchoActivity", input);
            using var cancellation = new CancellationTokenSource();
            Task timer = context.CreateTimer(TimeSpan.FromHours(24), cancellation.Token);
            Task<string> finish = context.WaitForExternalEvent<string>("Finish");
            context.SetCustomStatus("waiting");
            if (await Task.WhenAny(timer, finish) == timer)
            {
                await timer;
                return "timed out";
            }

            cancellation.Cancel();
            return $"{result}:{await finish}";
        }
    }

    /// <summary>Captures test diagnostics while preserving framework assertions and output.</summary>
    /// <param name="inner">The framework implementor to forward to.</param>
    private sealed class CapturedOutput(TestFrameworkImplementor inner) : TestFrameworkImplementor
    {
        /// <summary>Gets captured output lines.</summary>
        public ConcurrentQueue<string?> Lines { get; } = new();

        /// <summary>Gets captured output as text.</summary>
        public string Text => string.Join(Environment.NewLine, Lines);

        /// <inheritdoc/>
        public override void WriteLine(string? message)
        {
            Lines.Enqueue(message);
            inner.WriteLine(message);
        }

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default
            => inner.AssertAreEqual(expected, actual, message);

        /// <inheritdoc/>
        public override void AssertFail(string? message) => inner.AssertFail(message);

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => inner.AssertInconclusive(message);
    }
}
