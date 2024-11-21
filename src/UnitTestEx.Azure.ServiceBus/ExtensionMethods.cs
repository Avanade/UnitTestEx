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
    /// Provides the <b>UnitTestEx</b> extension methods.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(this TesterBase tester, T value) => CreateServiceBusMessageFromJson(tester, tester.JsonSerializer.Serialize(value));

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="value">The value.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(this TesterBase tester, T value, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromJson(tester, tester.JsonSerializer.Serialize(value), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromResource<TAssembly>(this TesterBase tester, string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromResource(tester, resourceName, messageModify, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromResource(this TesterBase tester, string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null, Assembly? assembly = null)
            => CreateServiceBusMessageFromJson(tester, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the serialized <paramref name="json"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="json">The JSON body.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
#if NET7_0_OR_GREATER
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromJson(this TesterBase tester, [StringSyntax(StringSyntaxAttribute.Json)] string json, Action<AmqpAnnotatedMessage>? messageModify = null)
#else
        public static ServiceBusReceivedMessage CreateServiceBusMessageFromJson(this TesterBase tester, string json, Action<AmqpAnnotatedMessage>? messageModify = null)
#endif
        {
            var message = new AmqpAnnotatedMessage(AmqpMessageBody.FromData([Encoding.UTF8.GetBytes(json ?? throw new ArgumentNullException(nameof(json)))]));
            message.Properties.ContentType = MediaTypeNames.Application.Json;
            message.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());
            return CreateServiceBusMessage(tester, message, messageModify);
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="message">The <see cref="ServiceBusMessage"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, ServiceBusMessage message)
            => CreateServiceBusMessage(tester, (message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="message">The <see cref="ServiceBusMessage"/>.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, ServiceBusMessage message, Action<AmqpAnnotatedMessage>? messageModify)
            => CreateServiceBusMessage(tester, (message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="message">The <see cref="AmqpAnnotatedMessage"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, AmqpAnnotatedMessage message) => CreateServiceBusMessage(tester, message, null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="message">The <see cref="AmqpAnnotatedMessage"/>.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needed to act as an extension method.")]
        public static ServiceBusReceivedMessage CreateServiceBusMessage(this TesterBase tester, AmqpAnnotatedMessage message, Action<AmqpAnnotatedMessage>? messageModify)
        {
            message.Header.DeliveryCount ??= 1;
            message.Header.Durable ??= true;
            message.Header.Priority ??= 1;
            message.Header.TimeToLive ??= TimeSpan.FromSeconds(60);

            messageModify?.Invoke(message);

            var t = typeof(ServiceBusReceivedMessage);
            var c = t.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [typeof(AmqpAnnotatedMessage)], null);
            return c == null
                ? throw new InvalidOperationException($"'{typeof(ServiceBusReceivedMessage).Name}' constructor that accepts Type '{typeof(AmqpAnnotatedMessage).Name}' parameter was not found.")
                : (ServiceBusReceivedMessage)c.Invoke([message]);
        }
    }
}