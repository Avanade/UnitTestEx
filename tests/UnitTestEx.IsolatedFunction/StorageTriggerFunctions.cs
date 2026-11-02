using Microsoft.Azure.Functions.Worker;

namespace UnitTestEx.IsolatedFunction;

public sealed class StorageTriggerFunctions
{
    [Function("BlobTrigger")]
    [BlobOutput("processed/result.txt", Connection = "AzureWebJobsStorage")]
    public static string ProcessBlob(
        [BlobTrigger("unittestex", Connection = "AzureWebJobsStorage", Source = BlobTriggerSource.EventGrid)] string content)
        => content;

    [Function("QueuePersonTrigger")]
    public static string ProcessQueuePerson(
        [QueueTrigger("unittestex", Connection = "AzureWebJobsStorage")] Person person)
        => $"{person.FirstName} {person.LastName}";

    [Function("QueueStringTrigger")]
    public static string ProcessQueueString(
        [QueueTrigger("unittestex-string", Connection = "AzureWebJobsStorage")] string message)
        => message;
}
