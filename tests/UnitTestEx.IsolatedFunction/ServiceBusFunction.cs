using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace UnitTestEx.IsolatedFunction
{
    public class ServiceBusFunction
    {
        private readonly HttpClient _httpClient;

        public ServiceBusFunction(IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("XXX");
        }

        [Function("ServiceBusFunction")]
        public async Task Run([ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            var id = message.Body.ToObjectFromJson<int>();
            await DoStuff(id).ConfigureAwait(false);
        }

        private async Task DoStuff(int id)
        {
            var hr = await _httpClient.GetAsync($"products/{id}").ConfigureAwait(false);
            hr.EnsureSuccessStatusCode();
        }
    }
}