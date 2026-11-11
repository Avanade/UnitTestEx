using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.DurableFunction;

public sealed class WorkflowActivities(IWorkflowJournal journal)
{
    [Function(nameof(ProcessItem))]
    public async Task<ItemResult> ProcessItem([ActivityTrigger] WorkItem item, FunctionContext context)
    {
        if (item.Amount < 0)
            throw new ArgumentOutOfRangeException(nameof(item), "Amount must not be negative.");

        context.GetLogger<WorkflowActivities>().LogInformation("Processing item {Id}.", item.Id);
        await Task.Yield();
        return new ItemResult(item.Id, item.Amount * 2);
    }

    [Function(nameof(RecordWorkflow))]
    public void RecordWorkflow([ActivityTrigger] string reference) => journal.Record(reference);

    [Function(nameof(PublishWorkflow))]
    public Task PublishWorkflow([ActivityTrigger] BatchResult result) => journal.PublishAsync(result);
}
