// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.WebJobs.ServiceBus;
using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the <b>UnitTestEx</b> extension methods.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusMessageActionsAssertor"/> as the <see cref="ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/>.</returns>
        public static WebJobsServiceBusMessageActionsAssertor CreateWebJobsServiceBusMessageActions(this TesterBase tester) => new(tester.Implementor);

        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/> as the <see cref="ServiceBusSessionMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="sessionLockedUntil">The sessions locked until <see cref="DateTimeOffset"/>; defaults to <see cref="DateTimeOffset.UtcNow"/> plus five minutes.</param>
        /// <param name="sessionState">The session state <see cref="BinaryData"/>; defaults to <see cref="BinaryData.Empty"/>.</param>
        /// <returns>The <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/>.</returns>
        public static WebJobsServiceBusSessionMessageActionsAssertor CreateWebJobsServiceBusSessionMessageActions(this TesterBase tester, DateTimeOffset? sessionLockedUntil = default, BinaryData? sessionState = default) => new(tester.Implementor, sessionLockedUntil, sessionState);

        /// <summary>
        /// Creates a <see cref="WorkerServiceBusMessageActionsAssertor"/> as the <see cref="Microsoft.Azure.Functions.Worker.ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="WorkerServiceBusMessageActionsAssertor"/>.</returns>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        public static WorkerServiceBusMessageActionsAssertor CreateWorkerServiceBusMessageActions(this TesterBase tester) => new(tester.Implementor);
    }
}