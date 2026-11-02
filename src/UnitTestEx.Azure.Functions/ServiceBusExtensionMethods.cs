// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides Service Bus message test helpers.
    /// </summary>
    public static class ServiceBusExtensionMethods
    {
        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the body contains the <paramref name="value"/> serialized as JSON.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(this TesterBase tester, T value)
            => CreateServiceBusMessageFromJson(tester, tester.JsonSerializer.Serialize(value));

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the body contains the <paramref name="value"/> serialized as JSON.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(this TesterBase tester, T value, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromJson(tester, tester.JsonSerializer.Serialize(value), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from a JSON embedded resource.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromResource<TAssembly>(this TesterBase tester, string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromResource(tester, resourceName, messageModify, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from a JSON embedded resource.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromResource(this TesterBase tester, string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null, Assembly? assembly = null)
            => CreateServiceBusMessageFromJson(tester, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from JSON.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromJson(this TesterBase tester, [StringSyntax(StringSyntaxAttribute.Json)] string json, Action<AmqpAnnotatedMessage>? messageModify = null)
        {
            ArgumentNullException.ThrowIfNull(json);
            var message = new AmqpAnnotatedMessage(AmqpMessageBody.FromData([Encoding.UTF8.GetBytes(json)]));
            message.Properties.ContentType = MediaTypeNames.Application.Json;
            message.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());
            return CreateServiceBusMessage(tester, message, messageModify);
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from a <see cref="ServiceBusMessage"/>.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, ServiceBusMessage message)
            => CreateServiceBusMessage(tester, (message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from a <see cref="ServiceBusMessage"/>.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, ServiceBusMessage message, Action<AmqpAnnotatedMessage>? messageModify)
            => CreateServiceBusMessage(tester, (message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from an AMQP message.
        /// </summary>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, AmqpAnnotatedMessage message)
            => CreateServiceBusMessage(tester, message, null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from an AMQP message.
        /// </summary>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needed to act as an extension method.")]
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, AmqpAnnotatedMessage message, Action<AmqpAnnotatedMessage>? messageModify)
        {
            ArgumentNullException.ThrowIfNull(message);
            message.Header.DeliveryCount ??= 1;
            message.Header.Durable ??= true;
            message.Header.Priority ??= 1;
            message.Header.TimeToLive ??= TimeSpan.FromSeconds(60);

            messageModify?.Invoke(message);

            return ServiceBusReceivedMessage.FromAmqpMessage(message, BinaryData.Empty);
        }
    }
}
