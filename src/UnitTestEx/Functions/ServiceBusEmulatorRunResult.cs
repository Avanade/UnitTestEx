// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Represents the result of a <see cref="ServiceBusEmulatorTester{TFunction}.RunAsync(TimeSpan?, bool?)"/>.
    /// </summary>
    public class ServiceBusEmulatorRunResult
    {
        /// <summary>
        /// Gets the <see cref="ServiceBusReceivedMessage"/> where received.
        /// </summary>
        public ServiceBusReceivedMessage? Message { get; internal set; }

        /// <summary>
        /// Gets the <see cref="ServiceBusMessageActionStatus"/>.
        /// </summary>
        public ServiceBusMessageActionStatus Status { get; internal set; } = ServiceBusMessageActionStatus.None;

        /// <summary>
        /// Gets the <see cref="ServiceBusMessageActionsWrapper.DeadLetterMessageAsync(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage, string, string?, System.Threading.CancellationToken)"/> <i>reason</i> where specified.
        /// </summary>
        public string? DeadletterReason { get; private set; }

        /// <summary>
        /// Gets the <see cref="ServiceBusReceivedMessage"/> properties that were explicitly modified via the <see cref="ServiceBusMessageActionsWrapper"/> methods.
        /// </summary>
        public IDictionary<string, object>? MessagePropertiesModified { get; private set; }

        /// <summary>
        /// Set properties using the <paramref name="actions"/> state.
        /// </summary>
        /// <param name="actions">The <see cref="ServiceBusMessageActionsWrapper"/>.</param>
        internal void SetUsingActionsWrapper(ServiceBusMessageActionsWrapper actions)
        {
            Status = actions.Status;
            DeadletterReason = actions.DeadletterReason;
            MessagePropertiesModified = actions.PropertiesModified;
        }
    }
}