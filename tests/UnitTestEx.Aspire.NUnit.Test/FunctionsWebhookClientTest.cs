using Azure.Messaging.EventGrid;
using NUnit.Framework;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Aspire.NUnit.Test
{
    [TestFixture]
    public class FunctionsWebhookClientTest
    {
        [Test]
        public void Constructor_RejectsNullHttpClient()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new FunctionsWebhookClient((HttpClient)null!));
            Assert.That(exception!.ParamName, Is.EqualTo("httpClient"));
        }

        [TestCase("blob", null)]
        [TestCase("blobs", "")]
        [TestCase("eventGrid", "/subscriptions/test/topics/events")]
        public async Task ExistingHttpClient_PostsEventsAndRemainsCallerOwned(string operation, string? topic)
        {
            var eventGridEvent = new EventGridEvent("test-subject", "test-event", "1.0", BinaryData.FromObjectAsJson(new { value = "payload" }));
            if (topic is not null)
                eventGridEvent.Topic = topic;

            var route = operation == "eventGrid" ? "EventGrid" : "blobs";
            var count = operation == "blobs" ? 2 : 1;
            using var handler = new TestHandler(async (request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo($"http://localhost:7071/runtime/webhooks/{route}?functionName=Host.Functions.Test%20%26%2F"));
                Assert.That(request.Headers.GetValues("aeg-event-type"), Is.EqualTo(new[] { "Notification" }));
                Assert.That(request.Headers.GetValues("x-test"), Is.EqualTo(new[] { "caller-header" }));
                Assert.That(request.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
                Assert.That(request.Content.Headers.ContentType.CharSet, Is.EqualTo("utf-8"));
                using var payload = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
                Assert.That(payload.RootElement.GetArrayLength(), Is.EqualTo(count));
                foreach (var item in payload.RootElement.EnumerateArray())
                {
                    Assert.That(item.GetProperty("id").GetString(), Is.EqualTo(eventGridEvent.Id));
                    Assert.That(item.GetProperty("topic").GetString(), Is.EqualTo(string.IsNullOrEmpty(topic) ? "UnitTestEx" : topic));
                    Assert.That(item.GetProperty("subject").GetString(), Is.EqualTo(eventGridEvent.Subject));
                    Assert.That(item.GetProperty("eventType").GetString(), Is.EqualTo(eventGridEvent.EventType));
                    Assert.That(item.GetProperty("eventTime").GetDateTimeOffset(), Is.EqualTo(eventGridEvent.EventTime));
                    Assert.That(item.GetProperty("dataVersion").GetString(), Is.EqualTo(eventGridEvent.DataVersion));
                    Assert.That(item.GetProperty("metadataVersion").GetString(), Is.EqualTo("1"));
                    Assert.That(item.GetProperty("data").GetProperty("value").GetString(), Is.EqualTo("payload"));
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("accepted") };
            });
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
            httpClient.DefaultRequestHeaders.Add("x-test", "caller-header");
            var webhookClient = new FunctionsWebhookClient(httpClient);

            for (var i = 0; i < 2; i++)
            {
                using var response = operation switch
                {
                    "blob" => await webhookClient.PostBlobCreatedAsync("Host.Functions.Test &/", eventGridEvent),
                    "blobs" => await webhookClient.PostBlobCreatedEventsAsync("Host.Functions.Test &/", [eventGridEvent, eventGridEvent]),
                    _ => await webhookClient.PostEventGridAsync("Host.Functions.Test &/", [eventGridEvent])
                };
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("accepted"));
                Assert.That(handler.IsDisposed, Is.False);
            }

            Assert.That(httpClient.BaseAddress, Is.EqualTo(new Uri("http://localhost:7071/")));
            Assert.That(httpClient.DefaultRequestHeaders.GetValues("x-test").Single(), Is.EqualTo("caller-header"));
            httpClient.Dispose();
            Assert.That(handler.IsDisposed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ExistingHttpClient_RemainsUsableAfterFailure(bool cancel)
        {
            using var cancellation = new CancellationTokenSource();
            using var handler = new TestHandler((_, token) =>
            {
                if (cancel)
                {
                    Assert.That(token.CanBeCanceled, Is.True);
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                throw new HttpRequestException("Simulated failure.");
            });
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
            var webhookClient = new FunctionsWebhookClient(httpClient);
            var eventGridEvent = new EventGridEvent("subject", "event", "1.0", BinaryData.FromObjectAsJson(new { }));

            if (cancel)
                Assert.CatchAsync<OperationCanceledException>(() => webhookClient.PostBlobCreatedAsync("Host.Functions.Test", eventGridEvent, cancellation.Token));
            else
                Assert.ThrowsAsync<HttpRequestException>(() => webhookClient.PostBlobCreatedAsync("Host.Functions.Test", eventGridEvent));

            Assert.That(handler.IsDisposed, Is.False);
            handler.OnSend = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            using var response = await httpClient.GetAsync("health");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSend { get; set; } = send;

            public bool IsDisposed { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => OnSend(request, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    IsDisposed = true;

                base.Dispose(disposing);
            }
        }
    }
}
