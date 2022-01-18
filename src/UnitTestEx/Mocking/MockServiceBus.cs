// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using Moq;
using Newtonsoft.Json;
using System;
using System.Text;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides Azure Service Bus mocking.
    /// </summary>
    public static class MockServiceBus
    {
        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> <see cref="Mock{T}"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Mock<ServiceBusReceivedMessage> CreateReceivedMessage<T>(T value)
        {
            var mock = new Mock<ServiceBusReceivedMessage>();
            mock.Setup(x => x.Body).Returns(new BinaryData(Encoding.Default.GetBytes(JsonConvert.SerializeObject(value))));
            return mock;
        }
    }
}