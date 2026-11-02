using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.Aspire.ServiceBus.Function;

public sealed class ServiceBusEchoFunction(ILogger<ServiceBusEchoFunction> logger)
{
    [Function("ServiceBusEcho")]
    [ServiceBusOutput("processed", Connection = "ServiceBusConnectionString")]
    public string Run([ServiceBusTrigger("input", Connection = "ServiceBusConnectionString")] string message)
    {
        logger.LogInformation("Processed Service Bus message: {Message}", message);
        return message;
    }
}
