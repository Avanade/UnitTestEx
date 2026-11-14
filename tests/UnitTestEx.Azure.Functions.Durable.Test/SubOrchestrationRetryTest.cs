using System.Collections.Concurrent;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies SDK-host retry behavior for sub-orchestrations.
/// </summary>
[TestFixture]
public sealed class SubOrchestrationRetryTest
{
    /// <summary>Verifies a child retry can recover and that exhaustion leaves failure metadata.</summary>
    [Test]
    public async Task ChildRetryRecoversAndExhaustionReportsFailure()
    {
        var attempts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        DurableTaskTestHost? host = null;

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "RetryingChildParent",
                    (context, input) => context.CallSubOrchestratorAsync<string>(
                        "RetryableChild",
                        input,
                        new SubOrchestrationOptions(
                            TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromMilliseconds(10))),
                            $"child-{input}")));
                registry.AddOrchestratorFunc<string, string>(
                    "RetryableChild",
                    async (context, input) =>
                    {
                        string attemptValue = await context.CallActivityAsync<string>("RecordChildAttempt", input);
                        int attempt = int.Parse(attemptValue, System.Globalization.CultureInfo.InvariantCulture);
                        if (input == "exhaust" || attempt < 3)
                            throw new WorkflowRejectedException($"Child {input} attempt {attempt} failed.");

                        return "Recovered";
                    });
                registry.AddActivityFunc<string, string>(
                    "RecordChildAttempt",
                    (_, input) => Task.FromResult(attempts.AddOrUpdate(input, 1, static (_, count) => count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            });

            var recoveryId = await host.Client.ScheduleNewOrchestrationInstanceAsync("RetryingChildParent", "recover");
            var recoveryMetadata = await host.Client.WaitForInstanceCompletionAsync(recoveryId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(recoveryMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(recoveryMetadata.ReadOutputAs<string>(), Is.EqualTo("Recovered"));
            Assert.That(attempts["recover"], Is.EqualTo(3));

            var exhaustedId = await host.Client.ScheduleNewOrchestrationInstanceAsync("RetryingChildParent", "exhaust");
            var exhaustedMetadata = await host.Client.WaitForInstanceCompletionAsync(exhaustedId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(exhaustedMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            Assert.That(attempts["exhaust"], Is.EqualTo(3));
            var failure = exhaustedMetadata.FailureDetails
                ?? throw new AssertionException("Exhausted child retries should produce parent failure details.");
            Assert.That(failure.ErrorType, Is.EqualTo(typeof(TaskFailedException).FullName));
            var innerFailure = failure.InnerFailure
                ?? throw new AssertionException("The child failure should be nested in the parent failure details.");
            Assert.That(innerFailure.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
            Assert.That(innerFailure.ErrorMessage, Is.EqualTo("Child exhaust attempt 3 failed."));
            Assert.That(innerFailure.IsCausedBy<WorkflowRejectedException>(), Is.True);
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Uses an asynchronous custom child retry handler with a durable timer and a bounded attempt count.</summary>
    /// <param name="recover">Whether the second child attempt succeeds.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task CustomChildRetryAwaitsDurableTimerAndRespectsAttemptLimit(bool recover)
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var responses = ActivityMock.InOrder(
            ActivityResponse.Next<string, string>(_ => throw new WorkflowRejectedException("First child attempt failed.")),
            ActivityResponse.Next<string, string>(_ => recover ? "recovered" : throw new WorkflowRejectedException("Second child attempt failed.")));
        await using var run = await functions.DurableTask()
            .Orchestrator<string>("CustomChildRetryParent", context => context.CallSubOrchestratorAsync<string>("CustomRetryChild", "same", TaskOptions.FromRetryHandler(async retry =>
            {
                retry.OrchestrationContext.SetCustomStatus($"retry:{retry.LastAttemptNumber}");
                TaskFailureDetails? failure = retry.LastFailure;
                while (failure is not null && !failure.IsCausedBy<WorkflowRejectedException>())
                    failure = failure.InnerFailure;

                if (retry.LastAttemptNumber >= 2 || failure is null)
                    return false;

                await retry.OrchestrationContext.CreateTimer(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                return true;
            })))
            .SubOrchestrator<string, string>("CustomRetryChild", (context, input) => context.CallActivityAsync<string>("ChildAttempt", input))
            .Activity<string, string>("ChildAttempt", responses.InvokeAsync, configure: options => options.Times(Times.Exactly(2)))
            .StartAsync();

        var result = await run.WaitForCompletionAsync();
        if (recover)
            result.AssertCompleted().AssertOutput("recovered");
        else
            result.AssertFailed().AssertFailureCausedBy<WorkflowRejectedException>();

        Assert.That(result.Metadata.ReadCustomStatusAs<string>(), Is.EqualTo(recover ? "retry:1" : "retry:2"));
        responses.Verify();
        run.Verify();
    }
}
