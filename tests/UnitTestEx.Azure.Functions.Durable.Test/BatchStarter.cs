using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Text.Json;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>Starts a batch orchestration for a received Service Bus message.</summary>
public sealed class BatchStarter
{
    /// <summary>Schedules the batch using its reference as the orchestration instance ID.</summary>
    /// <param name="message">The message containing a serialized batch request.</param>
    /// <param name="actions">The Service Bus message settlement actions.</param>
    /// <param name="client">The Functions-bound Durable client.</param>
    /// <returns>A task that completes after scheduling and settlement.</returns>
    [Function(nameof(StartBatch))]
    public async Task StartBatch([ServiceBusTrigger("batches", Connection = "ServiceBusConnectionString", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions, [DurableClient] DurableTaskClient client)
    {
        var request = message.Body.ToObjectFromJson<BatchRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Batch request is required.");
        await client.ScheduleNewOrchestrationInstanceAsync(nameof(BatchOrchestration.ProcessBatch), request, new StartOrchestrationOptions { InstanceId = request.Reference });
        await actions.CompleteMessageAsync(message);
    }
}
