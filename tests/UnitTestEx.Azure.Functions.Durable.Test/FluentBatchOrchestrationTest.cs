using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies fan-out/fan-in orchestration behavior through the fluent SDK-host API.
/// </summary>
[TestFixture]
public sealed class FluentBatchOrchestrationTest
{
    /// <summary>Verifies concurrent activity starts, ordered fan-in, and child finalization.</summary>
    [Test]
    public async Task FansOutActivitiesAndFansInBeforeFinalization()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        WorkItem[] items = [new("A", 10m), new("B", 20m), new("C", 30m)];
        var activityStarted = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var releaseActivity = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var activityCompleted = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var allActivitiesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aggregatedItems = new TaskCompletionSource<ItemResult[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizationInput = new TaskCompletionSource<(string InstanceId, BatchResult Input)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionOrder = new ConcurrentQueue<string>();
        int startedCount = 0;
        var request = new BatchRequest(" batch-7 ", items);
        ItemResult[] expectedItems = [new("A", 20m), new("B", 40m), new("C", 60m)];
        var expectedResult = new BatchResult("BATCH-7", 120m, expectedItems);

        await using var run = await functions.DurableTask()
            .Orchestrator<BatchRequest, BatchResult>(
                nameof(BatchOrchestration.ProcessBatch),
                (context, _) => BatchOrchestration.ProcessBatch(context))
            .Activity<string, string>(
                nameof(ActivityTransforms.NormalizeReference),
                (_, reference) => ActivityTransforms.NormalizeReference(reference),
                configure: options => options.Times(Times.Once()))
            .Activity<WorkItem, ItemResult>(
                nameof(WorkflowActivities.ProcessItem),
                async (_, item) =>
                {
                    if (activityStarted[item.Id].TrySetResult()
                        && Interlocked.Increment(ref startedCount) == items.Length)
                        allActivitiesStarted.TrySetResult();

                    await releaseActivity[item.Id].Task.ConfigureAwait(false);
                    completionOrder.Enqueue(item.Id);
                    activityCompleted[item.Id].TrySetResult();
                    return new ItemResult(item.Id, item.Amount * 2m);
                },
                configure: options => options.Times(Times.Exactly(3)))
            .Activity<ItemResult[], decimal>(
                nameof(ActivityTransforms.TotalItems),
                (_, results) =>
                {
                    aggregatedItems.TrySetResult(results.ToArray());
                    return ActivityTransforms.TotalItems(results);
                },
                configure: options => options.Times(Times.Once()))
            .SubOrchestrator<BatchResult, BatchResult>(
                nameof(BatchOrchestration.FinalizeBatch),
                async (context, input) =>
                {
                    finalizationInput.TrySetResult((context.InstanceId, input));
                    await context.WaitForExternalEvent<string>("ReleaseFinalization");
                    return input;
                })
            .StartAsync(request);

        try
        {
            Task<DurableTaskTestAssertor<BatchResult>> completion = run.WaitForCompletionAsync(TimeSpan.FromSeconds(10));
            await allActivitiesStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            releaseActivity["C"].TrySetResult();
            await activityCompleted["C"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseActivity["B"].TrySetResult();
            await activityCompleted["B"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.ThrowsAsync<TimeoutException>(async () =>
                await run.WaitForCustomStatusAsync(IsFinalizing, TimeSpan.FromMilliseconds(100)));

            releaseActivity["A"].TrySetResult();
            await activityCompleted["A"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(completionOrder, Is.EqualTo(new[] { "C", "B", "A" }));
            Assert.That(await aggregatedItems.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(expectedItems));

            var finalizingMetadata = await run.WaitForCustomStatusAsync(IsFinalizing);
            Assert.That(finalizingMetadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));
            var finalization = await finalizationInput.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var childInput = finalization.Input;
            Assert.That(childInput.Reference, Is.EqualTo(expectedResult.Reference));
            Assert.That(childInput.Total, Is.EqualTo(expectedResult.Total));
            Assert.That(childInput.Items, Is.EqualTo(expectedItems));
            Assert.That(completion.IsCompleted, Is.False);

            await run.Host.Client.RaiseEventAsync(finalization.InstanceId, "ReleaseFinalization", "release");
            (await completion).AssertCompleted().AssertOutput(expectedResult);
            run.Verify();
        }
        finally
        {
            foreach (TaskCompletionSource release in releaseActivity.Values)
                release.TrySetResult();
        }
    }

    /// <summary>Verifies that explicit run verification fails when a registered activity is not invoked.</summary>
    [Test]
    public async Task VerifyFailsWhenRegisteredActivityWasNotCalled()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string>("NoActivityOrchestration", _ => Task.FromResult("completed"))
            .Activity<string, string>("UnusedActivity", (_, input) => input)
            .StartAsync();

        (await run.WaitForCompletionAsync()).AssertCompleted();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(run.Verify)!;
        Assert.That(error.Message, Does.Contain("UnusedActivity"));
    }

    /// <summary>Returns whether the orchestration has entered the finalizing stage.</summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <returns>True when the custom status identifies the finalizing stage.</returns>
    private static bool IsFinalizing(OrchestrationMetadata metadata)
    {
        if (metadata.SerializedCustomStatus is null)
            return false;

        JsonElement status = metadata.ReadCustomStatusAs<JsonElement>();
        return status.ValueKind == JsonValueKind.Object
            && status.TryGetProperty("Stage", out JsonElement stage)
            && stage.GetString() == "Finalizing";
    }
}
