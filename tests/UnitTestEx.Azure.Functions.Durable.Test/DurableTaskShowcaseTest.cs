using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>Shows how to test a real batch orchestration with controlled activity responses.</summary>
/// <remarks>
/// Executes the real ProcessBatch, NormalizeReference and TotalItems bodies. ProcessItem responses are mocked;
/// FinalizeBatch is replaced with a pass-through child, so RecordWorkflow and PublishWorkflow are not invoked.
/// See the sample bodies in BatchOrchestration, ActivityTransforms and WorkflowActivities for the full workflow.
/// </remarks>
[TestFixture]
public sealed class DurableTaskShowcaseTest
{
    /// <summary>Verifies that fan-in preserves input order when activity handlers finish in reverse order.</summary>
    [Test]
    public async Task BatchFanOutFanIn()
    {
        // Match by business input, not arrival order: the host may dispatch concurrent activities in any order.
        // A and B wait for the test; C finishes after a short real-time delay, not a virtual-clock advance.
        using var processItems = ActivityMock.ByMatch(
        [
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "A", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "B", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "C", item => new ItemResult(item.Id, item.Amount * 2m)).After(TimeSpan.FromMilliseconds(100))
        ]);

        // Use the application's normal startup and services without starting the Functions worker.
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var request = new BatchRequest(" batch-7 ", [new("A", 10m), new("B", 20m), new("C", 30m)]);

        // Run the real orchestration, normalization and total calculation through the in-memory SDK host.
        // Replace item processing and finalization; this test does not exercise those production bodies.
        await using var test = await functions.DurableTask()
            .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
            .Activity<string, string>(nameof(ActivityTransforms.NormalizeReference), (_, reference) => ActivityTransforms.NormalizeReference(reference), configure: options => options.Times(Times.Once()))
            .Activity<WorkItem, ItemResult>(nameof(WorkflowActivities.ProcessItem), processItems.InvokeAsync, configure: options => options.Times(Times.Exactly(3)))
            .Activity<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), (_, results) => ActivityTransforms.TotalItems(results), configure: options => options.Times(Times.Once()))
            .SubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), (_, input) => Task.FromResult(input))
            .StartAsync(request);

        test.ExpectSuccess().ExpectLogContains("Finalizing batch BATCH-7.");

        // All three plans must be assigned before releasing the held responses; they need not have completed.
        // Indexes identify declaration positions, so [2] always means C even if it was dispatched first.
        await processItems.WaitForAllInvocationsAsync();
        await processItems[2].WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        await processItems[1].ReleaseAsync(TimeSpan.FromSeconds(5));
        await processItems[0].ReleaseAsync(TimeSpan.FromSeconds(5));

        // Task.WhenAll must retain A/B/C input order despite the handlers finishing C/B/A.
        var expected = new BatchResult("BATCH-7", 120m, [new("A", 20m), new("B", 40m), new("C", 60m)]);
        await test.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput(expected));

        // This records handler completion, not the order in which the engine processed their results.
        Assert.That(processItems.CompletionOrder.Select(item => item.Id), Is.EqualTo(new[] { "C", "B", "A" }));

        // Verify plan consumption separately from registered activity counts; retries count as extra invocations.
        processItems.Verify();
        test.Verify();
    }
}
