using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;

namespace UnitTestEx.Function
{
    public class ServiceBusFunction
    {
        private readonly HttpClient _httpClient;

        public ServiceBusFunction(IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("XXX");
        }

        [FunctionName("ServiceBusFunction")]
        public async Task Run([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString")] Person p, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
                throw new InvalidOperationException("First name is required.");

            var resp = await _httpClient.PostAsync($"person", p, new JsonMediaTypeFormatter()).ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
        }

        [FunctionName("ServiceBusFunction2")]
        public async Task Run2([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message, ILogger log)
        {
            var p = message.Body.ToObjectFromJson<Person>();
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
                throw new InvalidOperationException("First name is required.");

            var resp = await _httpClient.PostAsync($"person", p, new JsonMediaTypeFormatter()).ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
        }

        [FunctionName("ServiceBusFunction3")]
        public async Task Run3([ServiceBusTrigger("unittestex", Connection = "ServiceBusConnectionString", AutoCompleteMessages = false)] ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, ILogger log)
        {
            var p = message.Body.ToObjectFromJson<Person>();
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
            {
                await messageActions.DeadLetterMessageAsync(message, "Validation error.", "First name is required.").ConfigureAwait(false);
                log.LogError("First name is required.");
                return;
            }

            if (p.FirstName == "zerodivision")
                throw new DivideByZeroException("Divide by zero is not a thing.");

            var resp = await _httpClient.PostAsync($"person", p, new JsonMediaTypeFormatter()).ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();

            await messageActions.CompleteMessageAsync(message).ConfigureAwait(false);
        }

        [FunctionName("ServiceBusFunction4")]
        public Task Run4([ServiceBusTrigger("%Run4QueueName%", Connection = "ServiceBusConnectionString")] Person p, string subject, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed {subject} message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
                throw new InvalidOperationException("First name is required.");

            return Task.CompletedTask;
        }

        [FunctionName("ServiceBusSessionFunction5")]
        public Task Run5([ServiceBusTrigger("unittestexsess", Connection = "ServiceBusConnectionString2", IsSessionsEnabled = true)] ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, ILogger log)
        {
            return Task.CompletedTask;
        }
    }
}