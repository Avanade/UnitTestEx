// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using CoreEx.Json;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides an expected event publisher to support <see cref="EventExpectations"/>.
    /// </summary>
    /// <remarks>Where an <see cref="ILogger"/> is provided then each <see cref="EventData"/> will also be logged during <i>Send</i>.</remarks>
    public sealed class ExpectedEventPublisher : EventPublisher
    {
        private readonly TestSharedState _sharedState;
        private readonly ILogger? _logger;
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Get the <c>null</c> key name.
        /// </summary>
        public const string NullKeyName = "<default>";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectedEventPublisher"/> class.
        /// </summary>
        /// <param name="sharedState">The <see cref="TestSharedState"/>.</param>
        /// <param name="logger">The optional <see cref="ILogger"/> for logging the events (each <see cref="EventData"/>).</param>
        /// <param name="jsonSerializer">The optional <see cref="IJsonSerializer"/> for the logging. Defaults to <see cref="JsonSerializer.Default"/></param>
        /// <param name="eventDataFormatter">The <see cref="EventDataFormatter"/>; defaults where not specified.</param>
        public ExpectedEventPublisher(TestSharedState sharedState, ILogger<ExpectedEventPublisher>? logger = null, IJsonSerializer? jsonSerializer = null, EventDataFormatter? eventDataFormatter = null)
            : base(eventDataFormatter, new CoreEx.Text.Json.EventDataSerializer(), new NullEventSender())
        {
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _sharedState.ExpectedEventPublisher = this;
            _logger = logger;
            _jsonSerializer = jsonSerializer ?? JsonSerializer.Default;
        }


        /// <summary>
        /// Gets the dictionary that contains the sent events by destination.
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentQueue<EventData>> SentEvents { get; } = new();

        /// <inheritdoc/>
        protected override Task OnEventSendAsync(string? name, EventData eventData, EventSendData eventSendData, CancellationToken cancellationToken)
        {
            var queue = SentEvents.GetOrAdd(name ?? NullKeyName, _ => new ConcurrentQueue<EventData>());
            queue.Enqueue(eventData);

            if (_logger != null)
            {
                var sb = new StringBuilder("Event send");
                if (!string.IsNullOrEmpty(name))
                    sb.Append($" (destination: '{name}')");

                sb.AppendLine(" ->");

                var json = _jsonSerializer.Serialize(eventData, JsonWriteFormat.Indented);
                sb.Append(json);
                _logger.LogInformation("{Event}", sb.ToString());
            }

            return Task.CompletedTask;
        }
    }
}