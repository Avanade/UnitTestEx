using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
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
        public async Task Run([ServiceBusTrigger("myqueue", Connection = "ServiceBusConnectionString")] Person p, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
                throw new InvalidOperationException("First name is required.");

            var resp = await _httpClient.PostAsync($"person", p, new JsonMediaTypeFormatter()).ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
        }

        [FunctionName("ServiceBusFunction2")]
        public async Task Run2([ServiceBusTrigger("myqueue", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message, ILogger log)
        {
            var p = message.Body.ToObjectFromJson<Person>();
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {p.FirstName} {p.LastName}");

            if (p.FirstName == null)
                throw new InvalidOperationException("First name is required.");

            var resp = await _httpClient.PostAsync($"person", p, new JsonMediaTypeFormatter()).ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
        }
    }
}