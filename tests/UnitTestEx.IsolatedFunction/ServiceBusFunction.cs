using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace UnitTestEx.IsolatedFunction;

public sealed class ServiceBusFunction
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceBusFunction> _logger;

    public ServiceBusFunction(IHttpClientFactory clientFactory, ILogger<ServiceBusFunction> logger)
    {
        _httpClient = clientFactory.CreateClient("XXX");
        _logger = logger;
    }

    [Function("ServiceBusFunction")]
    public async Task Run([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString")] Person person)
    {
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {FirstName} {LastName}", person.FirstName, person.LastName);
        if (person.FirstName is null)
            throw new InvalidOperationException("First name is required.");

        using var response = await _httpClient.PostAsJsonAsync("person", person, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    [Function("ServiceBusFunction2")]
    public async Task Run2([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message)
    {
        var person = message.Body.ToObjectFromJson<Person>();
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {FirstName} {LastName}", person?.FirstName, person?.LastName);
        if (person is null || person.FirstName is null)
            throw new InvalidOperationException("First name is required.");

        using var response = await _httpClient.PostAsJsonAsync("person", person, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    [Function("ServiceBusFunction3")]
    public async Task Run3([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString", AutoCompleteMessages = false)] ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
    {
        var person = message.Body.ToObjectFromJson<Person>();
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {FirstName} {LastName}", person?.FirstName, person?.LastName);
        if (person is null || person.FirstName is null)
        {
            await messageActions.DeadLetterMessageAsync(message, null, "Validation error.", "First name is required.").ConfigureAwait(false);
            _logger.LogError("First name is required.");
            return;
        }

        if (person.FirstName == "zerodivision")
            throw new DivideByZeroException("Divide by zero is not a thing.");

        using var response = await _httpClient.PostAsJsonAsync("person", person, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await messageActions.CompleteMessageAsync(message).ConfigureAwait(false);
    }

    [Function("ServiceBusFunction4")]
    public static Task Run4([ServiceBusTrigger("%Run4QueueName%", Connection = "ServiceBusConnectionString")] Person person)
    {
        if (person.FirstName is null)
            throw new InvalidOperationException("First name is required.");

        return Task.CompletedTask;
    }

    [Function("ServiceBusSessionFunction5")]
    public Task Run5([ServiceBusTrigger("unittestexsess", Connection = "ServiceBusConnectionString", IsSessionsEnabled = true)] ServiceBusReceivedMessage _)
        => Task.CompletedTask;
}