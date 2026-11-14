using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies class-based Durable Task orchestrator registration in the in-memory host.
/// </summary>
[TestFixture]
public sealed class ClassBasedOrchestrationTest
{
    /// <summary>Registers and runs a typed TaskOrchestrator through the host's public registry API.</summary>
    [Test]
    public async Task ExecutesTypedTaskOrchestrator()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        DurableTaskTestHost? host = null;

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
            {
                registry.AddOrchestrator<ClassBasedEchoOrchestrator>("ClassBasedEcho");
                registry.AddActivityFunc<string, string>("ClassBasedEchoActivity", (_, input) => $"processed:{input}");
            });

            string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ClassBasedEcho", "test-input");
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

            Assert.That(metadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<string>(), Is.EqualTo("processed:test-input"));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Verifies a failed activity in a class-based orchestrator is represented in host metadata.</summary>
    [Test]
    public async Task ClassBasedOrchestratorFailureRetainsNestedActivityDetails()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        DurableTaskTestHost? host = null;

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
            {
                registry.AddOrchestrator<ClassBasedFailureOrchestrator>("ClassBasedFailure");
                registry.AddActivityFunc<string, string>(
                    "ClassBasedFailureActivity",
                    (_, input) => Task.FromException<string>(new WorkflowRejectedException($"Class-based failure for {input}.")));
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ClassBasedFailure", "request-1");
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            var failure = metadata.FailureDetails
                ?? throw new AssertionException("Class-based orchestration failure metadata should be populated.");
            Assert.That(failure.ErrorType, Is.EqualTo(typeof(TaskFailedException).FullName));
            Assert.That(failure.InnerFailure?.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
            Assert.That(failure.InnerFailure?.ErrorMessage, Is.EqualTo("Class-based failure for request-1."));
            Assert.That(failure.InnerFailure?.IsCausedBy<WorkflowRejectedException>(), Is.True);
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Exposes typed orchestration failures through fluent assertions and managed expectations.</summary>
    [Test]
    public async Task FluentTypedOrchestratorRetainsNestedActivityFailure()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator("FluentClassBasedFailure", new ClassBasedFailureOrchestrator())
            .Activity<string, string>("ClassBasedFailureActivity", (_, input) => Task.FromException<string>(new WorkflowRejectedException($"Class-based failure for {input}.")),
                configure: options => options.Times(Times.Once()))
            .StartAsync("request-1");

        run.ExpectException().Type<DurableTaskOrchestrationException>("Class-based failure for request-1.");
        var result = await run.WaitForCompletionAsync();
        result.AssertFailed().AssertFailureCausedBy<WorkflowRejectedException>();
        Assert.That(result.FailureDetails?.InnerFailure?.ErrorMessage, Is.EqualTo("Class-based failure for request-1."));
        run.Verify();
    }

    /// <summary>Registers a typed child through both input and no-input parent builders.</summary>
    /// <param name="withInput">Whether the parent is started with input.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task FluentParentsInvokeTypedChild(bool withInput)
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var child = new ClassBasedEchoOrchestrator();
        await using var run = withInput
            ? await functions.DurableTask()
                .Orchestrator<string, string>("InputParent", (context, input) => context.CallSubOrchestratorAsync<string>("TypedChild", input))
                .SubOrchestrator("TypedChild", child)
                .Activity<string, string>("ClassBasedEchoActivity", (_, input) => $"processed:{input}", configure: options => options.Times(Times.Once()))
                .StartAsync("hello")
            : await functions.DurableTask()
                .Orchestrator<string>("NoInputParent", context => context.CallSubOrchestratorAsync<string>("TypedChild", "hello"))
                .SubOrchestrator("TypedChild", child)
                .Activity<string, string>("ClassBasedEchoActivity", (_, input) => $"processed:{input}", configure: options => options.Times(Times.Once()))
                .StartAsync();

        await run.WaitForCompletionAsync(assert: result => result.AssertCompleted().AssertOutput("processed:hello"));
        run.Verify();
    }
}
