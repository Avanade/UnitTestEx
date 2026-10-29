using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.IsolatedFunction;

public sealed class EventHubsFunctions(ILogger<EventHubsFunctions> logger)
{
    [Function("EventHubSingle")]
    public string Single([EventHubTrigger("events", Connection = "EventHubsConnection", IsBatched = false)] EventData message)
    {
        logger.LogInformation("Received event {SequenceNumber} for {PartitionKey}.", message.SequenceNumber, message.PartitionKey);
        return message.EventBody.ToString();
    }

    [Function("EventHubBatch")]
    public Task<string[]> Batch([EventHubTrigger("events", Connection = "EventHubsConnection")] EventData[] messages)
    {
        logger.LogInformation("Received {Count} events.", messages.Length);
        return Task.FromResult(messages.Select(message => message.EventBody.ToString()).ToArray());
    }

    [Function("EventHubFailure")]
    public void Fail([EventHubTrigger("events", Connection = "EventHubsConnection", IsBatched = false)] EventData message)
        => throw new InvalidOperationException($"Rejected event {message.SequenceNumber}.");
}
