using Microsoft.Azure.Functions.Worker;

namespace UnitTestEx.DurableFunction;

/// <summary>Provides stateless activity bodies for normalization, aggregation and failure examples.</summary>
public static class ActivityTransforms
{
    /// <summary>Trims a nonblank reference and converts it to invariant uppercase.</summary>
    /// <param name="reference">The caller-supplied reference, which may contain surrounding whitespace.</param>
    /// <returns>The normalized reference, such as <c>BATCH-7</c>.</returns>
    /// <exception cref="ArgumentException">The reference is null, empty or whitespace.</exception>
    [Function(nameof(NormalizeReference))]
    public static string NormalizeReference([ActivityTrigger] string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return reference.Trim().ToUpperInvariant();
    }

    /// <summary>Sums the amounts returned by item activities without changing their order or values.</summary>
    /// <param name="items">The processed item results, not the original work items.</param>
    /// <returns>The batch total; the showcase sums 20, 40 and 60 to produce 120.</returns>
    [Function(nameof(TotalItems))]
    public static decimal TotalItems([ActivityTrigger] ItemResult[] items) => items.Sum(item => item.Amount);

    /// <summary>Fails asynchronously for the separate orchestration failure/recovery examples.</summary>
    /// <param name="reference">The reference included in the rejection message.</param>
    /// <returns>A task that fails rather than producing an output.</returns>
    /// <exception cref="WorkflowRejectedException">The example always rejects the workflow.</exception>
    [Function(nameof(RejectWorkflow))]
    public static async Task<string> RejectWorkflow([ActivityTrigger] string reference)
    {
        await Task.Yield();
        throw new WorkflowRejectedException($"Workflow {reference} was declined.");
    }
}
