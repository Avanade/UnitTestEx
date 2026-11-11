using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.DurableFunction;

public sealed class BatchOrchestration
{
    [Function(nameof(ProcessBatch))]
    public static async Task<BatchResult> ProcessBatch([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<BatchRequest>() ?? throw new ArgumentException("Batch input is required.");
        var reference = await context.CallActivityAsync<string>(nameof(ActivityTransforms.NormalizeReference), input.Reference);
        var options = TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(1)));
        var tasks = input.Items.Select(item => context.CallActivityAsync<ItemResult>(nameof(WorkflowActivities.ProcessItem), item, options)).ToArray();
        var items = await Task.WhenAll(tasks);
        var total = await context.CallActivityAsync<decimal>(nameof(ActivityTransforms.TotalItems), items);
        context.SetCustomStatus(new { Stage = "Finalizing", At = context.CurrentUtcDateTime });
        context.CreateReplaySafeLogger<BatchOrchestration>().LogInformation("Finalizing batch {Reference}.", reference);
        return await context.CallSubOrchestratorAsync<BatchResult>(
            nameof(FinalizeBatch), new BatchResult(reference, total, items),
            new SubOrchestrationOptions { InstanceId = context.NewGuid().ToString() });
    }

    [Function(nameof(FinalizeBatch))]
    public async Task<BatchResult> FinalizeBatch([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<BatchResult>() ?? throw new ArgumentException("Batch result is required.");
        await context.CallActivityAsync(nameof(WorkflowActivities.RecordWorkflow), input.Reference);
        await context.CallActivityAsync(nameof(WorkflowActivities.PublishWorkflow), input);
        return input;
    }
}
