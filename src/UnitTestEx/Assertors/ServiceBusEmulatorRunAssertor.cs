// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;
using UnitTestEx.Functions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the test result assert helper for a <see cref="ServiceBusEmulatorRunResult"/>.
    /// </summary>
    public class ServiceBusEmulatorRunAssertor : AssertorBase<ServiceBusEmulatorRunAssertor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResultAssertor{TResult}"/> class.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="exception">The <see cref="Exception"/> (if any).</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal ServiceBusEmulatorRunAssertor(ServiceBusEmulatorRunResult result, Exception? exception, TestFrameworkImplementor implementor) : base(exception, implementor) => Result = result;

        /// <summary>
        /// Gets the <see cref="ServiceBusEmulatorRunResult"/>.
        /// </summary>
        public ServiceBusEmulatorRunResult Result { get; }

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> matches the <paramref name="expectedStatus"/>.
        /// </summary>
        /// <param name="expectedStatus">The expected <see cref="ServiceBusMessageActionStatus"/>.</param>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertStatus(ServiceBusMessageActionStatus expectedStatus)
        {
            Implementor.AssertAreEqual(expectedStatus, Result.Status, "ServiceBusMessageActionStatus value is not equal.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> is <see cref="ServiceBusMessageActionStatus.None"/>.
        /// </summary>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertNoMessageStatus() => AssertStatus(ServiceBusMessageActionStatus.None);

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> is <see cref="ServiceBusMessageActionStatus.Completed"/>.
        /// </summary>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertMessageCompleted() => AssertStatus(ServiceBusMessageActionStatus.Completed);

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> is <see cref="ServiceBusMessageActionStatus.Deferred"/>.
        /// </summary>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertMessageDeferred() => AssertStatus(ServiceBusMessageActionStatus.Deferred);

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> is <see cref="ServiceBusMessageActionStatus.Deadlettered"/>.
        /// </summary>
        /// <param name="expectedReason">Optional expected reason that will be validated against actual.</param>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertMessageDeadlettered(string? expectedReason = null)
        {
            AssertStatus(ServiceBusMessageActionStatus.Deadlettered);
            if (expectedReason != null)
                Implementor.AssertAreEqual(expectedReason, Result.DeadletterReason, "DeadletterReason value is not equal.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="ServiceBusEmulatorRunResult.Status"/> is <see cref="ServiceBusMessageActionStatus.Abandoned"/>.
        /// </summary>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/> to support fluent-style method-chaining.</returns>
        public ServiceBusEmulatorRunAssertor AssertMessageAbandoned() => AssertStatus(ServiceBusMessageActionStatus.Abandoned);
    }
}