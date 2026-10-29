// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.EventGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Posts Event Grid notifications to the runtime webhook routes exposed by an Azure Functions resource.
    /// </summary>
    public sealed class FunctionsWebhookClient
    {
        private readonly Func<Task<HttpClient>> _httpClientFactory;

        /// <summary>
        /// Creates a webhook client that obtains its HTTP client from the specified factory.
        /// </summary>
        /// <param name="httpClientFactory">The factory that creates an HTTP client for the Functions resource.</param>
        internal FunctionsWebhookClient(Func<Task<HttpClient>> httpClientFactory) => _httpClientFactory = httpClientFactory;

        /// <summary>
        /// Posts a BlobCreated event to the Azure Functions blob-trigger webhook.
        /// </summary>
        /// <param name="functionName">The Functions host function name (e.g. <c>Host.Functions.CustomerImport</c>).</param>
        /// <param name="blobCreatedEvent">The BlobCreated event.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The webhook response; the caller remains responsible for asserting the status and disposing the response.</returns>
        public Task<HttpResponseMessage> PostBlobCreatedAsync(string functionName, EventGridEvent blobCreatedEvent, CancellationToken cancellationToken = default)
            => PostBlobCreatedEventsAsync(functionName, [blobCreatedEvent], cancellationToken);

        /// <summary>
        /// Posts one or more events to the Azure Functions blob-trigger webhook.
        /// </summary>
        /// <param name="functionName">The Functions host function name (e.g. <c>Host.Functions.CustomerImport</c>).</param>
        /// <param name="events">The events to post.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The webhook response; the caller remains responsible for asserting the status and disposing the response.</returns>
        public Task<HttpResponseMessage> PostBlobCreatedEventsAsync(string functionName, IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default)
            => PostEventsAsync("runtime/webhooks/blobs", functionName, events, cancellationToken);

        /// <summary>
        /// Posts one or more events to the Azure Functions Event Grid trigger webhook.
        /// </summary>
        /// <param name="functionName">The Functions host function name (e.g. <c>Host.Functions.MyEventGridFunction</c>).</param>
        /// <param name="events">The events to post.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The webhook response; the caller remains responsible for asserting the status and disposing the response.</returns>
        public Task<HttpResponseMessage> PostEventGridAsync(string functionName, IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default)
            => PostEventsAsync("runtime/webhooks/EventGrid", functionName, events, cancellationToken);

        /// <summary>
        /// Serializes and posts Event Grid-schema notifications to a Functions runtime webhook route.
        /// </summary>
        /// <param name="route">The relative runtime webhook route.</param>
        /// <param name="functionName">The host-qualified function name.</param>
        /// <param name="events">The events to serialize and post.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HTTP response from the Functions host.</returns>
        private async Task<HttpResponseMessage> PostEventsAsync(string route, string functionName, IEnumerable<EventGridEvent> events, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(functionName);
            ArgumentNullException.ThrowIfNull(events);

            var eventArray = events.ToArray();
            if (eventArray.Length == 0)
                throw new ArgumentException("At least one event is required.", nameof(events));
            if (eventArray.Any(e => e is null))
                throw new ArgumentException("Events must not contain null values.", nameof(events));

            var payload = JsonSerializer.Serialize(eventArray.Select(ToEventGridSchemaEvent));
            var requestUri = $"{route}?functionName={Uri.EscapeDataString(functionName)}";

            using var client = await _httpClientFactory().ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("aeg-event-type", "Notification");
            return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Converts an SDK event into the Event Grid schema payload expected by the Functions runtime webhook.
        /// </summary>
        /// <param name="eventGridEvent">The SDK Event Grid event.</param>
        /// <returns>An object containing the Event Grid schema fields and cloned event data.</returns>
        private static object ToEventGridSchemaEvent(EventGridEvent eventGridEvent)
        {
            using var data = JsonDocument.Parse(eventGridEvent.Data.ToString());
            return new
            {
                id = eventGridEvent.Id,
                topic = "UnitTestEx",
                subject = eventGridEvent.Subject,
                eventType = eventGridEvent.EventType,
                eventTime = eventGridEvent.EventTime,
                dataVersion = eventGridEvent.DataVersion,
                metadataVersion = "1",
                data = data.RootElement.Clone()
            };
        }
    }
}
