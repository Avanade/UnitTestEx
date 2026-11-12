using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.DurableFunction.HostProbe;

/// <summary>
/// Records how the pinned in-memory host handles non-durable await patterns.
/// </summary>
[TestFixture]
public sealed class InvalidAwaitProbe
{
    /// <summary>Observes the host outcome for the selected non-durable await pattern.</summary>
    /// <param name="orchestrationName">The orchestration registered for this case.</param>
    /// <param name="pattern">The pattern described in the test output.</param>
    /// <param name="expectedOutcome">The expected terminal or watchdog outcome.</param>
    [TestCase("TaskDelayProbe", "Task.Delay", "TimedOut")]
    [TestCase("TaskRunProbe", "Task.Run with a durable call", "TimedOut")]
    [TestCase("ConfigureAwaitProbe", "ConfigureAwait(false)", "TimedOut")]
    [TestCase("DirectIoProbe", "direct file read that changes across replay", "Failed")]
    public async Task RecordsHowPinnedHostHandlesNonDurableAwaitPatterns(string orchestrationName, string pattern, string expectedOutcome)
    {
        DurableTaskTestHost? host = null;
        string? path = null;

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<string, string>(
                    "TaskDelayProbe",
                    async (context, input) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(20));
                        return await context.CallActivityAsync<string>("Echo", input);
                    });

                registry.AddOrchestratorFunc<string, string>(
                    "TaskRunProbe",
                    async (context, input) =>
                        await Task.Run(async () => await context.CallActivityAsync<string>("Echo", input)));

                registry.AddOrchestratorFunc<string, string>(
                    "ConfigureAwaitProbe",
                    async (context, input) =>
                    {
                        string first = await context.CallActivityAsync<string>("Echo", input).ConfigureAwait(false);
                        return await context.CallActivityAsync<string>("Echo", first);
                    });

                registry.AddOrchestratorFunc<string, string>(
                    "DirectIoProbe",
                    async (context, filePath) =>
                    {
                        string activityName = File.ReadAllText(filePath);
                        return await context.CallActivityAsync<string>(activityName, filePath);
                    });

                registry.AddActivityFunc<string, string>("Echo", (_, input) => input);
                registry.AddActivityFunc<string, string>(
                    "ReadA",
                    (_, filePath) =>
                    {
                        File.WriteAllText(filePath, "ReadB");
                        return "A";
                    });

                registry.AddActivityFunc<string, string>("ReadB", (_, _) => "B");
            });

            string input = "probe-input";
            if (orchestrationName == "DirectIoProbe")
            {
                path = Path.Combine(Path.GetTempPath(), $"unittestex-invalid-await-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(path, "ReadA");
                input = path;
            }

            string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, input);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            OrchestrationMetadata metadata;
            try
            {
                metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, true, timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                TestContext.Progress.WriteLine($"{pattern}: TIMED OUT - no terminal orchestration state within five seconds.");
                Assert.That(expectedOutcome, Is.EqualTo("TimedOut"));
                return;
            }

            string observation = metadata.RuntimeStatus.ToString();
            if (metadata.FailureDetails is { } failure)
                observation += $" - {failure.ErrorType}: {failure.ErrorMessage}";
            else if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                observation += $" - {metadata.ReadOutputAs<string>()}";

            TestContext.Progress.WriteLine($"{pattern}: {observation}");
            Assert.That(metadata.RuntimeStatus.ToString(), Is.EqualTo(expectedOutcome));
            if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                Assert.That(metadata.FailureDetails, Is.Not.Null, "Failed orchestration should include a diagnostic.");
                if (orchestrationName == "DirectIoProbe")
                {
                    Assert.That(metadata.FailureDetails!.ErrorType, Does.Contain("NonDeterministicOrchestrationException"));
                    Assert.That(metadata.FailureDetails.ErrorMessage, Does.Contain("ReadA"));
                    Assert.That(metadata.FailureDetails.ErrorMessage, Does.Contain("ReadB"));
                }
            }
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
            if (path is not null && File.Exists(path))
                File.Delete(path);
        }
    }
}
