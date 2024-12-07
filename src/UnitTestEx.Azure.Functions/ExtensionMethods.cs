// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
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
        internal const string HttpMethodCheckName = "HttpTriggerTester_MethodCheck";
        internal const string HttpRouteCheckOptionName = "HttpTriggerTester_" + nameof(RouteCheckOption);
        internal const string HttpRouteComparisonName = "HttpTriggerTester_" + nameof(StringComparison);

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

        /// <summary>
        /// Sets the default that <i>no</i> check is performed to ensure that the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute.Methods"/> or <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contains the <see cref="HttpRequest.Method"/> for the <see cref="HttpTriggerTester{TFunction}.WithNoMethodCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithNoMethodCheck(this TestSetUp setup)
        {
            setup.Properties[HttpMethodCheckName] = false;
            return setup;
        }

        /// <summary>
        /// Sets the default that a check is performed to ensure that the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute.Methods"/> or <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contains the <see cref="HttpRequest.Method"/> for the <see cref="HttpTriggerTester{TFunction}.WithMethodCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithMethodCheck(this TestSetUp setup)
        {
            setup.Properties[HttpMethodCheckName] = true;
            return setup;
        }

        /// <summary>
        /// Sets the default <see cref="RouteCheckOption"/> to be <see cref="RouteCheckOption.None"/> for the <see cref="HttpTriggerTester{TFunction}.WithNoRouteCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithNoHttpRouteCheck(this TestSetUp setup) => WithHttpRouteCheck(setup, RouteCheckOption.None);

        /// <summary>
        /// Sets the default <see cref="RouteCheckOption"/> to be checked during execution for the <see cref="HttpTriggerTester{TFunction}.WithRouteCheck(RouteCheckOption, StringComparison?)"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        /// <param name="option">The <see cref="RouteCheckOption"/>.</param>
        /// <param name="comparison">The <see cref="StringComparison"/>.</param>
        public static TestSetUp WithHttpRouteCheck(this TestSetUp setup, RouteCheckOption option, StringComparison? comparison = StringComparison.OrdinalIgnoreCase)
        {
            setup.Properties[HttpRouteCheckOptionName] = option;
            setup.Properties[HttpRouteComparisonName] = comparison;
            return setup;
        }

        /// <summary>
        /// Invokes the <see cref="HttpTriggerTester{TFunction}.WithNoMethodCheck"/> or <see cref="HttpTriggerTester{TFunction}.WithMethodCheck"/> method based on the <see cref="TestSetUp"/> <see cref="HttpMethodCheckName"/> property.
        /// </summary>
        /// <typeparam name="TFunction">The Azure Function <see cref="System.Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTriggerTester{TFunction}"/>.</param>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        internal static void SetHttpMethodCheck<TFunction>(this HttpTriggerTester<TFunction> tester, TestSetUp setup) where TFunction : class
        {
            if (!setup.Properties.TryGetValue(HttpMethodCheckName, out var check) || (bool)check! == true)
                tester.WithMethodCheck();
            else
                tester.WithNoMethodCheck();
        }

        /// <summary>
        /// Invokes the <see cref="HttpTriggerTester{TFunction}.WithRouteCheck"/> method to set the <see cref="RouteCheckOption"/> and <see cref="StringComparison"/> from the <see cref="TestSetUp"/>.
        /// </summary>
        /// <typeparam name="TFunction">The Azure Function <see cref="System.Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTriggerTester{TFunction}"/>.</param>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        internal static void SetHttpRouteCheck<TFunction>(this HttpTriggerTester<TFunction> tester, TestSetUp setup) where TFunction : class
            => tester.WithRouteCheck(
                setup.Properties.TryGetValue(HttpRouteCheckOptionName, out var option) ? (RouteCheckOption)option! : RouteCheckOption.PathAndQuery,
                setup.Properties.TryGetValue(HttpRouteComparisonName, out var comparison) ? (StringComparison)comparison! : StringComparison.OrdinalIgnoreCase);
    }
}