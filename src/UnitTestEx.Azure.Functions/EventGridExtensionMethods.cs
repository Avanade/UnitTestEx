// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging;
using Azure.Messaging.EventGrid;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides Event Grid event test helpers.
    /// </summary>
    public static class EventGridExtensionMethods
    {
        /// <summary>
        /// Creates a <c>Microsoft.Storage.BlobCreated</c> Event Grid event suitable for posting to an Azure Functions blob-trigger webhook.
        /// </summary>
        /// <param name="containerName">The blob container name.</param>
        /// <param name="blobName">The blob name, including any virtual directory path.</param>
        /// <param name="blobUri">The blob's absolute URI.</param>
        /// <param name="eTag">The blob's ETag.</param>
        /// <param name="contentType">The blob's content type.</param>
        /// <param name="contentLength">The blob's content length.</param>
        /// <returns>The BlobCreated event.</returns>
        public static EventGridEvent CreateBlobCreatedEvent(string containerName, string blobName, Uri blobUri, string eTag, string contentType, long contentLength)
        {
            ArgumentException.ThrowIfNullOrEmpty(containerName);
            ArgumentException.ThrowIfNullOrEmpty(blobName);
            ArgumentNullException.ThrowIfNull(blobUri);
            if (!blobUri.IsAbsoluteUri)
                throw new ArgumentException("The blob URI must be absolute.", nameof(blobUri));

            ArgumentException.ThrowIfNullOrEmpty(eTag);
            ArgumentException.ThrowIfNullOrEmpty(contentType);
            ArgumentOutOfRangeException.ThrowIfNegative(contentLength);

            var data = BinaryData.FromObjectAsJson(new
            {
                api = "PutBlob",
                url = blobUri.AbsoluteUri,
                eTag,
                contentType,
                contentLength,
                blobType = "BlockBlob",
                sequencer = "00000000000000000000000000000000"
            });

            var subject = $"/blobServices/default/containers/{containerName}/blobs/{blobName}";
            return new EventGridEvent(subject, "Microsoft.Storage.BlobCreated", "1.0", data);
        }

        /// <summary>
        /// Creates an <see cref="EventGridEvent"/> whose data contains the <paramref name="value"/> serialized as JSON.
        /// </summary>
        public static EventGridEvent CreateEventGridEventFromValue<T>(this TesterBase tester, T value, string subject, string eventType, string dataVersion = "1.0")
            => CreateEventGridEventFromJson(tester, tester.JsonSerializer.Serialize(value), subject, eventType, dataVersion);

        /// <summary>
        /// Creates an <see cref="EventGridEvent"/> from JSON.
        /// </summary>
        public static EventGridEvent CreateEventGridEventFromJson(this TesterBase tester, [StringSyntax(StringSyntaxAttribute.Json)] string json, string subject, string eventType, string dataVersion = "1.0")
        {
            ArgumentNullException.ThrowIfNull(json);
            return new EventGridEvent(subject, eventType, dataVersion, BinaryData.FromString(json));
        }

        /// <summary>
        /// Creates an <see cref="EventGridEvent"/> from a JSON embedded resource.
        /// </summary>
        public static EventGridEvent CreateEventGridEventFromResource<TAssembly>(this TesterBase tester, string resourceName, string subject, string eventType, string dataVersion = "1.0")
            => CreateEventGridEventFromResource(tester, resourceName, subject, eventType, dataVersion, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates an <see cref="EventGridEvent"/> from a JSON embedded resource.
        /// </summary>
        public static EventGridEvent CreateEventGridEventFromResource(this TesterBase tester, string resourceName, string subject, string eventType, string dataVersion = "1.0", Assembly? assembly = null)
            => CreateEventGridEventFromJson(tester, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), subject, eventType, dataVersion);

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> whose data contains the <paramref name="value"/> serialized as JSON.
        /// </summary>
        public static CloudEvent CreateCloudEventFromValue<T>(this TesterBase tester, T value, string source, string type)
            => CreateCloudEventFromJson(tester, tester.JsonSerializer.Serialize(value), source, type);

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from JSON.
        /// </summary>
        public static CloudEvent CreateCloudEventFromJson(this TesterBase tester, [StringSyntax(StringSyntaxAttribute.Json)] string json, string source, string type)
        {
            ArgumentNullException.ThrowIfNull(json);
            return new CloudEvent(source, type, BinaryData.FromString(json), "application/json", CloudEventDataFormat.Json);
        }

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from a JSON embedded resource.
        /// </summary>
        public static CloudEvent CreateCloudEventFromResource<TAssembly>(this TesterBase tester, string resourceName, string source, string type)
            => CreateCloudEventFromResource(tester, resourceName, source, type, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from a JSON embedded resource.
        /// </summary>
        public static CloudEvent CreateCloudEventFromResource(this TesterBase tester, string resourceName, string source, string type, Assembly? assembly = null)
            => CreateCloudEventFromJson(tester, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), source, type);
    }
}
