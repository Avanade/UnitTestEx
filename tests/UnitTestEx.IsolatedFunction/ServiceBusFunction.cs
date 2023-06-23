using Azure.Messaging.ServiceBus;
using CoreEx.Azure.ServiceBus;
using CoreEx.Events;
using CoreEx.Events.Subscribing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace UnitTestEx.IsolatedFunction
{
    public class ServiceBusFunction
    {
        private readonly ServiceBusSubscriber _subscriber;
        private readonly HttpClient _httpClient;

        public ServiceBusFunction(ServiceBusSubscriber subscriber, IHttpClientFactory clientFactory)
        {
            _subscriber = subscriber;
            _httpClient = clientFactory.CreateClient("XXX");
        }

        [Function("ServiceBusFunction")]
        public async Task Run([ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
            => await _subscriber.ReceiveAsync<int>(message, messageActions, DoStuff).ConfigureAwait(false);

        private async Task DoStuff(EventData<int> ed, EventSubscriberArgs args)
        {
            var hr = await _httpClient.GetAsync($"products/{ed.Value}").ConfigureAwait(false);
            hr.EnsureSuccessStatusCode();
        }
    }
}