// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Collections.Generic;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Represents the <see cref="ServiceBusMessageActionsWrapper"/> status.
    /// </summary>
    public enum ServiceBusMessageActionStatus
    {
        /// <summary>
        /// Indicates the no action occured.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActionsWrapper.AbandonMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Abandoned,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActionsWrapper.CompleteMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Completed,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActionsWrapper.DeadLetterMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> or
        /// <see cref="ServiceBusMessageActionsWrapper.DeadLetterMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, string, string?, System.Threading.CancellationToken)"/> were invoked.
        /// </summary>
        Deadlettered,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActionsWrapper.DeferMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Deferred
    }
}