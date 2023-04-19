// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Encapsulates the <see cref="ServiceBusMessageActions"/> functionality to enable support within the <see cref="ServiceBusEmulatorTester{TFunction}"/>.
    /// </summary>
    public class ServiceBusMessageActionsWrapper : ServiceBusMessageActions
    {
        private readonly ServiceBusReceiver _receiver;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusMessageActionsWrapper"/> class.
        /// </summary>
        /// <param name="receiver">The <see cref="ServiceBusReceiver"/>.</param>
        /// <param name="logger">The <see cref="ILogger"/>.</param>
        internal ServiceBusMessageActionsWrapper(ServiceBusReceiver receiver, ILogger logger)
        {
            _receiver = receiver;
            _logger = logger;
        }

        /// <summary>
        /// Gets the <see cref="ServiceBusMessageActionStatus"/>.
        /// </summary>
        public ServiceBusMessageActionStatus Status { get; private set; } = ServiceBusMessageActionStatus.None;

        /// <summary>
        /// Gets the <see cref="DeadLetterMessageAsync(ServiceBusReceivedMessage, string, string?, CancellationToken)"/> <i>reason</i> where specified.
        /// </summary>
        public string? DeadletterReason { get; private set; }

        /// <summary>
        /// Gets the properties of the message modified where specified.
        /// </summary>
        public IDictionary<string, object>? PropertiesModified { get; private set; }

        /// <inheritdoc/>
        public override async Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Service Bus message is being Abandoned.");
            await _receiver.AbandonMessageAsync(message, propertiesToModify, cancellationToken);
            Status = ServiceBusMessageActionStatus.Abandoned;
            PropertiesModified = propertiesToModify;
        }

        /// <inheritdoc/>
        public override async Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Service Bus message is being Completed.");
            await _receiver.CompleteMessageAsync(message, cancellationToken);
            Status = ServiceBusMessageActionStatus.Completed;
        }

        /// <inheritdoc/>
        public override async Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Service Bus message is being Dead Lettered.");
            await _receiver.DeadLetterMessageAsync(message, propertiesToModify, cancellationToken);
            Status = ServiceBusMessageActionStatus.Deadlettered;
            PropertiesModified = propertiesToModify;
        }

        /// <inheritdoc/>
        public override async Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Service Bus message is being Dead Lettered; Reason: {deadLetterReason}{(string.IsNullOrEmpty(deadLetterErrorDescription) ? "" : $"{Environment.NewLine}{deadLetterErrorDescription}")}");
            await _receiver.DeadLetterMessageAsync(message, deadLetterReason, deadLetterErrorDescription, cancellationToken);
            Status = ServiceBusMessageActionStatus.Deadlettered;
            DeadletterReason = deadLetterReason;
        }

        /// <inheritdoc/>
        public override async Task DeferMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Service Bus message is being Deferred.");
            await _receiver.DeferMessageAsync(message, propertiesToModify, cancellationToken);
            Status = ServiceBusMessageActionStatus.Deferred;
            PropertiesModified = propertiesToModify;
        }
    }
}