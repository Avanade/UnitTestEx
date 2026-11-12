using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnitTestEx;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.NUnit.Internal;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies timer forwarding and diagnostics with the in-memory Durable Task host.
/// </summary>
[TestFixture]
public sealed class HostTimerDiagnosticTest
{
    /// <summary>Verifies that a host timer is forwarded and diagnosed only before replay.</summary>
    [Test]
    public async Task ContextDecoratorForwardsTimerAndWarnsOnlyBeforeReplay()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>(createFactory: () => new NUnitTestImplementor());
        DurableTaskTestHost? host = null;
        int replayCount = 0;

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
            {
                registry.AddOrchestratorFuncWithTimerDiagnostics<string, string>(
                    nameof(TimerDiagnosticOrchestration.Run),
                    (context, _) =>
                    {
                        if (context.IsReplaying)
                            Interlocked.Increment(ref replayCount);
                        return TimerDiagnosticOrchestration.Run(context);
                    });
                registry.AddActivityFunc<string, string>(
                    nameof(TimerDiagnosticOrchestration.Complete),
                    (_, value) => value);
            });

            var elapsed = Stopwatch.StartNew();
            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(TimerDiagnosticOrchestration.Run), "completed");
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(metadata.ReadOutputAs<string>(), Is.EqualTo("completed"));
            Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50)),
                "The host timer should not be replaced with an immediately completed task.");
            Assert.That(replayCount, Is.GreaterThan(0), "The host should replay after the durable timer and activity complete.");

            var warnings = functions.SharedState.GetLoggerMessages()
                .Where(message => message?.Contains("uses the host's real wall clock", StringComparison.Ordinal) == true)
                .ToArray();
            Assert.That(warnings, Has.Length.EqualTo(1), "Timer diagnostics should not repeat during replay.");
            Assert.That(warnings[0], Does.Contain("00:00:00.1000000"));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }

    /// <summary>Contains orchestration and activity delegates used by the timer diagnostic test.</summary>
    private sealed class TimerDiagnosticOrchestration
    {
        /// <summary>Waits on a durable timer, then schedules the completion activity.</summary>
        /// <param name="context">The orchestration context.</param>
        /// <returns>The completion activity's result.</returns>
        public static async Task<string> Run(TaskOrchestrationContext context)
        {
            await context.CreateTimer(TimeSpan.FromMilliseconds(100), CancellationToken.None);
            return await context.CallActivityAsync<string>(nameof(Complete), context.GetInput<string>());
        }

        /// <summary>Returns the supplied value as the activity result.</summary>
        /// <param name="value">The value to return.</param>
        /// <returns>The supplied value.</returns>
        public static string Complete(string value) => value;
    }
}
