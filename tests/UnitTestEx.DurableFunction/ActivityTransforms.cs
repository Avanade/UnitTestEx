using Microsoft.Azure.Functions.Worker;

namespace UnitTestEx.DurableFunction;

public static class ActivityTransforms
{
    [Function(nameof(NormalizeReference))]
    public static string NormalizeReference([ActivityTrigger] string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return reference.Trim().ToUpperInvariant();
    }

    [Function(nameof(TotalItems))]
    public static decimal TotalItems([ActivityTrigger] ItemResult[] items) => items.Sum(item => item.Amount);

    [Function(nameof(RejectWorkflow))]
    public static async Task<string> RejectWorkflow([ActivityTrigger] string reference)
    {
        await Task.Yield();
        throw new WorkflowRejectedException($"Workflow {reference} was declined.");
    }
}
