using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies that the in-memory Durable Task host detects changed orchestration history.
/// </summary>
[TestFixture]
public sealed class ReplayDeterminismTest
{
    /// <summary>Verifies changing the activity name across replay produces a nondeterminism diagnostic.</summary>
    [Test]
    public async Task ChangedActivityNameAcrossReplayFailsWithNondeterminismDiagnostic()
    {
        DurableTaskTestHost? host = null;
        int orchestrationEntryCount = 0;

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "ReplayDeterminismProbe",
                    async (context, input) =>
                    {
                        string activityName = Interlocked.Increment(ref orchestrationEntryCount) == 1
                            ? "ReplayReadA"
                            : "ReplayReadB";
                        return await context.CallActivityAsync<string>(activityName, input);
                    });
                registry.AddActivityFunc<string, string>("ReplayReadA", (_, input) => input);
                registry.AddActivityFunc<string, string>("ReplayReadB", (_, input) => input);
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("ReplayDeterminismProbe", "replay-input");
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(Volatile.Read(ref orchestrationEntryCount), Is.GreaterThanOrEqualTo(2));
            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            Assert.That(metadata.FailureDetails?.ErrorType, Does.Contain("NonDeterministicOrchestrationException"));
            Assert.That(metadata.FailureDetails?.ErrorMessage, Does.Contain("ReplayReadA"));
            Assert.That(metadata.FailureDetails?.ErrorMessage, Does.Contain("ReplayReadB"));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
