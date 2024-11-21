// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides a <see cref="ServiceBusMessageActions"/> test mock and assert verification.
    /// </summary>
    /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
    public class WebJobsServiceBusMessageActionsAssertor(TestFrameworkImplementor implementor) : ServiceBusMessageActions
    {
        private readonly TestFrameworkImplementor _implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));

        /// <summary>
        /// Gets the <see cref="ServiceBusMessageActionStatus"/>.
        /// </summary>
        public ServiceBusMessageActionStatus Status { get; private set; } = ServiceBusMessageActionStatus.None;

        /// <summary>
        /// Gets the <see cref="DeadLetterMessageAsync(ServiceBusReceivedMessage, string, string?, CancellationToken)"/> <i>reason</i> where specified.
        /// </summary>
        public string? DeadLetterReason { get; private set; }

        /// <summary>
        /// Gets the <see cref="DeadLetterMessageAsync(ServiceBusReceivedMessage, string, string?, CancellationToken)"/> <i>error description</i> where specified.
        /// </summary>
        public string? DeadLetterErrorDescription { get; private set; }

        /// <summary>
        /// Gets the properties of the message modified where specified.
        /// </summary>
        public IDictionary<string, object>? PropertiesModified { get; private set; }

        /// <summary>
        /// Gets the <see cref="RenewMessageLockAsync(ServiceBusReceivedMessage, CancellationToken)"/> count.
        /// </summary>
        public int RenewCount { get; private set; }

        /// <inheritdoc/>
        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = default, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.Abandon);
            Status = ServiceBusMessageActionStatus.Abandon;
            PropertiesModified = propertiesToModify;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.Complete);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = default, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.DeadLetter);
            PropertiesModified = propertiesToModify;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason, string? deadLetterErrorDescription = default, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.DeadLetter);
            DeadLetterReason = deadLetterReason;
            DeadLetterErrorDescription = deadLetterErrorDescription;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, Dictionary<string, object> propertiesToModify, string deadLetterReason, string deadLetterErrorDescription = default!, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.DeadLetter);
            DeadLetterReason = deadLetterReason;
            DeadLetterErrorDescription = deadLetterErrorDescription;
            PropertiesModified = propertiesToModify;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task DeferMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.Defer);
            PropertiesModified = propertiesToModify;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            VerifyAndSetActionStatus(ServiceBusMessageActionStatus.Renew);
            RenewCount++;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Verifies action is a valid operation.
        /// </summary>
        private void VerifyAndSetActionStatus(ServiceBusMessageActionStatus status)
        {
            if (Status != ServiceBusMessageActionStatus.None)
                throw new InvalidOperationException($"The {status}MessageAsync cannot be invoked after a previous {Status}.");

            if (status != ServiceBusMessageActionStatus.Renew)
                Status = status;
        }

        /// <summary>
        /// Logs the result of the <see cref="WebJobsServiceBusMessageActionsAssertor"/>.
        /// </summary>
        internal void LogResult()
        {
            _implementor.WriteLine("");
            _implementor.WriteLine("MESSAGE ACTIONS >");
            _implementor.WriteLine($"Action: {Status}");

            if (RenewCount > 0)
                _implementor.WriteLine($"Renew count: {RenewCount}");

            if (Status == ServiceBusMessageActionStatus.DeadLetter)
            {
                _implementor.WriteLine($"Reason: {DeadLetterReason ?? "<null>"}");
                _implementor.WriteLine($"Description: {DeadLetterErrorDescription ?? "<null>"}");
            }

            _implementor.WriteLine($"Properties modified{(PropertiesModified is null ? ": None." : " >")}");
            if (PropertiesModified != null)
            {
                foreach (var pm in PropertiesModified)
                {
                    _implementor.WriteLine($"  {pm.Key}: {pm.Value ?? "<null>"}");
                }
            }
        }

        /// <summary>
        /// Assert the status.
        /// </summary>
        private WebJobsServiceBusMessageActionsAssertor AssertStatus(ServiceBusMessageActionStatus status)
        {
            if (!Status.Equals(status))
                _implementor.AssertAreEqual(status, Status, "ServiceBusMessageActions status is not equal.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="AbandonMessageAsync(ServiceBusReceivedMessage, IDictionary{string, object}?, CancellationToken)"/> was invoked.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertAbandon() => AssertStatus(ServiceBusMessageActionStatus.Abandon);

        /// <summary>
        /// Asserts that the <see cref="CompleteMessageAsync(ServiceBusReceivedMessage, CancellationToken)"/> was invoked.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertComplete() => AssertStatus(ServiceBusMessageActionStatus.Complete);

        /// <summary>
        /// Asserts that the <see cref="DeferMessageAsync(ServiceBusReceivedMessage, IDictionary{string, object}?, CancellationToken)"/> was invoked.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertDefer() => AssertStatus(ServiceBusMessageActionStatus.Defer);

        /// <summary>
        /// Asserts that the one of the <see cref="DeadLetterMessageAsync(ServiceBusReceivedMessage, string, string?, CancellationToken)"/> methods was invoked.
        /// </summary>
        /// <param name="reasonContains">Asserts that the resulting <see cref="DeadLetterReason"/> contains the specified content.</param>
        /// <param name="errorDescriptionContains">Asserts that the resulting <see cref="DeadLetterErrorDescription"/> contains the specified content.</param>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertDeadLetter(string? reasonContains = default, string? errorDescriptionContains = default)
        {
            AssertStatus(ServiceBusMessageActionStatus.DeadLetter);
            if (!string.IsNullOrEmpty(reasonContains))
            {
                if (string.IsNullOrEmpty(DeadLetterReason) || !DeadLetterReason.Contains(reasonContains, StringComparison.InvariantCultureIgnoreCase))
                    _implementor.AssertFail($"Expected the {nameof(DeadLetterReason)} to contain: {reasonContains}");
            }

            if (!string.IsNullOrEmpty(errorDescriptionContains))
            {
                if (string.IsNullOrEmpty(DeadLetterErrorDescription) || !DeadLetterErrorDescription.Contains(errorDescriptionContains, StringComparison.InvariantCultureIgnoreCase))
                    _implementor.AssertFail($"Expected the {nameof(DeadLetterErrorDescription)} to contain: {errorDescriptionContains}");
            }

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="RenewMessageLockAsync(ServiceBusReceivedMessage, CancellationToken)"/> was invoked at least once or <paramref name="count"/> specified.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertRenew(int? count = default)
        {
            if (count.HasValue)
                _implementor.AssertAreEqual(count.Value, RenewCount, $"{nameof(RenewCount)} is not equal.");
            else if (RenewCount == 0)
                _implementor.AssertFail($"Expected the {nameof(RenewCount)} to be greater than zero.");

            return this;
        }

        /// <summary>
        /// Asserts that <i>no</i> methods (with the exception of <see cref="RenewMessageLockAsync(ServiceBusReceivedMessage, CancellationToken)"/>) were invoked.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/> to support fluent-style method-chaining.</returns>
        public WebJobsServiceBusMessageActionsAssertor AssertNone() => AssertStatus(ServiceBusMessageActionStatus.None);
    }
}