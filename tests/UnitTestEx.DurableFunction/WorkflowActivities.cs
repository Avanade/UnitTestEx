using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.DurableFunction;

/// <summary>Provides item processing and journal activities for the batch workflow.</summary>
/// <param name="journal">The application service used by recording and publishing activities.</param>
public sealed class WorkflowActivities(IWorkflowJournal journal)
{
    /// <summary>Validates an item, logs its ID and returns the same ID with twice its original amount.</summary>
    /// <remarks>
    /// The asynchronous yield makes this a simple async activity example, not external work.
    /// The showcase supplies equivalent outputs through an activity mock, bypassing this validation and log.
    /// </remarks>
    /// <param name="item">The input work item.</param>
    /// <param name="context">The function context supplying the activity logger.</param>
    /// <returns>The processed item result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The input amount is negative.</exception>
    [Function(nameof(ProcessItem))]
    public async Task<ItemResult> ProcessItem([ActivityTrigger] WorkItem item, FunctionContext context)
    {
        if (item.Amount < 0)
            throw new ArgumentOutOfRangeException(nameof(item), "Amount must not be negative.");

        context.GetLogger<WorkflowActivities>().LogInformation("Processing item {Id}.", item.Id);
        await Task.Yield();
        return new ItemResult(item.Id, item.Amount * 2);
    }

    /// <summary>Records the normalized reference through the application's injected journal.</summary>
    /// <remarks>Called by the real finalization child; skipped when the showcase substitutes that child.</remarks>
    /// <param name="reference">The normalized workflow reference.</param>
    [Function(nameof(RecordWorkflow))]
    public void RecordWorkflow([ActivityTrigger] string reference) => journal.Record(reference);

    /// <summary>Publishes the aggregate through the application's injected journal and awaits its completion.</summary>
    /// <remarks>The sample journal only logs; it does not contact a broker or persist the result.</remarks>
    /// <param name="result">The completed batch aggregate.</param>
    /// <returns>The journal's publish task.</returns>
    [Function(nameof(PublishWorkflow))]
    public Task PublishWorkflow([ActivityTrigger] BatchResult result) => journal.PublishAsync(result);
}
