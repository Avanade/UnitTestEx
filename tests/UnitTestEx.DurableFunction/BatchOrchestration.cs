using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.DurableFunction;

/// <summary>Coordinates reference normalization, concurrent item processing, aggregation and finalization.</summary>
public sealed class BatchOrchestration
{
    /// <summary>Processes a batch and returns its normalized reference, total and input-ordered item results.</summary>
    /// <remarks>
    /// Input is read from the durable context. Item activities allow up to three attempts with a one-second
    /// initial retry interval; the showcase exercises the successful path, not those retries.
    /// The finalization child receives the aggregate and determines the parent's final output.
    /// </remarks>
    /// <param name="context">The orchestration context carrying a <see cref="BatchRequest"/>.</param>
    /// <returns>The finalized batch result.</returns>
    [Function(nameof(ProcessBatch))]
    public static async Task<BatchResult> ProcessBatch([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<BatchRequest>() ?? throw new ArgumentException("Batch input is required.");
        var reference = await context.CallActivityAsync<string>(nameof(ActivityTransforms.NormalizeReference), input.Reference);
        var options = TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(1)));

        // Materialize every durable call before awaiting so item activities can run concurrently.
        var tasks = input.Items.Select(item => context.CallActivityAsync<ItemResult>(nameof(WorkflowActivities.ProcessItem), item, options)).ToArray();

        // Fan-in preserves the task-list/input order, not the order activities finish.
        var items = await Task.WhenAll(tasks);
        var total = await context.CallActivityAsync<decimal>(nameof(ActivityTransforms.TotalItems), items);
        context.SetCustomStatus(new { Stage = "Finalizing", At = context.CurrentUtcDateTime });
        context.CreateReplaySafeLogger<BatchOrchestration>().LogInformation("Finalizing batch {Reference}.", reference);

        // Use the durable context's replay-safe clock/ID facilities, not DateTime.UtcNow or Guid.NewGuid.
        return await context.CallSubOrchestratorAsync<BatchResult>(
            nameof(FinalizeBatch), new BatchResult(reference, total, items),
            new SubOrchestrationOptions { InstanceId = context.NewGuid().ToString() });
    }

    /// <summary>Records the normalized reference, publishes the aggregate and returns the batch unchanged.</summary>
    /// <remarks>
    /// Journal interactions are activities, not side effects inside replayed orchestration code.
    /// The showcase replaces this child with a pass-through delegate, so neither journal activity runs there.
    /// </remarks>
    /// <param name="context">The child orchestration context carrying the aggregated <see cref="BatchResult"/>.</param>
    /// <returns>The input batch after both journal activities have completed.</returns>
    [Function(nameof(FinalizeBatch))]
    public async Task<BatchResult> FinalizeBatch([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<BatchResult>() ?? throw new ArgumentException("Batch result is required.");
        await context.CallActivityAsync(nameof(WorkflowActivities.RecordWorkflow), input.Reference);
        await context.CallActivityAsync(nameof(WorkflowActivities.PublishWorkflow), input);
        return input;
    }
}
