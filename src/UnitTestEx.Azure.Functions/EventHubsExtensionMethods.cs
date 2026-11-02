// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.EventHubs;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides received Event Hubs event builders for isolated-worker function tests.
    /// </summary>
    public static class EventHubsExtensionMethods
    {
        /// <summary>
        /// Creates received <see cref="EventData"/> containing a value serialized with the tester's JSON serializer.
        /// </summary>
        public static EventData CreateEventDataFromValue<T>(this TesterBase tester, T value, Action<EventData>? eventModify = null)
        {
            ArgumentNullException.ThrowIfNull(tester);
            return CreateEventDataFromJson(tester, tester.JsonSerializer.Serialize(value), eventModify);
        }

        /// <summary>
        /// Creates received <see cref="EventData"/> containing JSON.
        /// </summary>
        public static EventData CreateEventDataFromJson(this TesterBase tester, [StringSyntax(StringSyntaxAttribute.Json)] string json, Action<EventData>? eventModify = null)
        {
            ArgumentNullException.ThrowIfNull(json);
            return CreateEventData(tester, BinaryData.FromString(json), eventModify: eventModify);
        }

        /// <summary>
        /// Creates received <see cref="EventData"/> from an embedded JSON resource in the specified type's assembly.
        /// </summary>
        public static EventData CreateEventDataFromResource<TAssembly>(this TesterBase tester, string resourceName, Action<EventData>? eventModify = null)
            => CreateEventDataFromResource(tester, resourceName, eventModify, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates received <see cref="EventData"/> from an embedded JSON resource.
        /// </summary>
        public static EventData CreateEventDataFromResource(this TesterBase tester, string resourceName, Action<EventData>? eventModify = null, Assembly? assembly = null)
            => CreateEventDataFromJson(tester, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), eventModify);

        /// <summary>
        /// Creates received <see cref="EventData"/> with body bytes and broker-assigned metadata using the Azure SDK model factory.
        /// </summary>
        /// <param name="tester">The owning tester.</param>
        /// <param name="body">The event body.</param>
        /// <param name="sequenceNumber">The sequence number within a partition.</param>
        /// <param name="offset">The opaque offset within a partition.</param>
        /// <param name="enqueuedTime">The enqueue time; defaults to the current UTC time.</param>
        /// <param name="partitionKey">The optional partition key.</param>
        /// <param name="eventModify">An optional callback to set application properties, content type or correlation identifiers.</param>
        /// <returns>The received event.</returns>
        public static EventData CreateEventData(this TesterBase tester, BinaryData body, long sequenceNumber = 0, string offset = "0", DateTimeOffset? enqueuedTime = null, string? partitionKey = null, Action<EventData>? eventModify = null)
        {
            ArgumentNullException.ThrowIfNull(tester);
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(sequenceNumber);

            var data = EventHubsModelFactory.EventData(body, sequenceNumber: sequenceNumber, offsetString: offset, enqueuedTime: enqueuedTime ?? DateTimeOffset.UtcNow, partitionKey: partitionKey);
            eventModify?.Invoke(data);
            return data;
        }
    }
}
