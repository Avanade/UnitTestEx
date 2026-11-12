using System.Collections.Concurrent;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;
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
}
