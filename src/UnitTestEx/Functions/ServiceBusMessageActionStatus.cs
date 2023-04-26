// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.WebJobs.ServiceBus;
using System.Collections.Generic;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Represents the <see cref="ServiceBusMessageActionsWrapper"/> status.
    /// </summary>
    public enum ServiceBusMessageActionStatus
    {
        /// <summary>
        /// Indicates that no action occured.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActions.AbandonMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Abandon,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActions.CompleteMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Complete,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActions.DeadLetterMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> or
        /// <see cref="ServiceBusMessageActionsWrapper.DeadLetterMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, string, string?, System.Threading.CancellationToken)"/> were invoked.
        /// </summary>
        DeadLetter,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActions.DeferMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, IDictionary{string, object}?, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Defer,

        /// <summary>
        /// Indicates that the <see cref="ServiceBusMessageActions.RenewMessageLockAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, System.Threading.CancellationToken)"/> was invoked.
        /// </summary>
        Renew
    }
}