using Microsoft.Extensions.Logging;

namespace UnitTestEx.DurableFunction;

public interface IWorkflowJournal
{
    void Record(string reference);
    Task PublishAsync(BatchResult result);
}

public sealed class WorkflowJournal(ILogger<WorkflowJournal> logger) : IWorkflowJournal
{
    public void Record(string reference) => logger.LogInformation("Recorded workflow {Reference}.", reference);

    public Task PublishAsync(BatchResult result)
    {
        logger.LogInformation("Published workflow {Reference} with total {Total}.", result.Reference, result.Total);
        return Task.CompletedTask;
    }
}
