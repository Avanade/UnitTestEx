// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Abstractions;
using CoreEx.Entities;
using CoreEx.Events;
using System;
using System.Linq;
using System.Net;
using System.Text;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="HttpResponseExpectations"/> functionality for an <see cref="HttpTesterBase{TSelf}"/>.
    /// </summary>
    public static class ExpectationsExtensions
    {
        /// <summary>
        /// Trys to match the <paramref name="expected"/> and <paramref name="actual"/> <see cref="ApiError"/> contents.
        /// </summary>
        /// <param name="expected">The expected <see cref="MessageItemCollection"/>.</param>
        /// <param name="actual">The actual <see cref="MessageItemCollection"/>.</param>
        /// <param name="errorMessage">The error message where they do not match.</param>
        /// <returns><c>true</c> where matched; otherwise, <c>false</c>.</returns>
        public static bool TryAreMessagesMatched(MessageItemCollection? expected, MessageItemCollection? actual, out string? errorMessage)
        {
            var exp = (from e in expected ?? new MessageItemCollection()
                       where !(actual ?? new MessageItemCollection()).Any(a => a.Type == e.Type && a.Text == e.Text && (e.Property == null || a.Property == e.Property))
                       select e).ToList();

            var act = (from a in actual ?? new MessageItemCollection()
                       where !(expected ?? new MessageItemCollection()).Any(e => a.Type == e.Type && a.Text == e.Text && (e.Property == null || a.Property == e.Property))
                       select a).ToList();

            var sb = new StringBuilder();
            if (exp.Count > 0)
            {
                sb.AppendLine(" Expected messages not matched:");
                exp.ForEach(m => sb.AppendLine($"  {m.Type}: {m.Text} {(m.Property != null ? $"[{m.Property}]" : null)}"));
            }

            if (act.Count > 0)
            {
                sb.AppendLine(" Actual messages not matched:");
                act.ForEach(m => sb.AppendLine($"  {m.Type}: {m.Text} {(m.Property != null ? $"[{m.Property}]" : null)}"));
            }

            errorMessage = sb.Length > 0 ? $"Messages mismatch:{System.Environment.NewLine}{sb}" : null;
            return sb.Length == 0;
        }

        #region IHttpResponseExpectations<TSelf>

        /// <summary>
        /// Invokes the set expectation logic.
        /// </summary>
        private static TSelf SetHttpResponseExpectation<TSelf>(this IHttpResponseExpectations<TSelf> tester, Action<IHttpResponseExpectations<TSelf>> action) where TSelf : IHttpResponseExpectations<TSelf>
        {
            action(tester);
            return (TSelf)tester;
        }

        /// <summary>
        /// Expects a response with the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectStatusCode<TSelf>(this IHttpResponseExpectations<TSelf> tester, HttpStatusCode statusCode = HttpStatusCode.OK) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectStatusCode(statusCode));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorType"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="errorType">The <see cref="ErrorType"/>.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TSelf>(this IHttpResponseExpectations<TSelf> tester, ErrorType errorType, string? errorMessage = null) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectErrorType((int)errorType, errorMessage));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorCode"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TSelf>(this IHttpResponseExpectations<TSelf> tester, int errorCode, string? errorMessage = null) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectErrorType(errorCode, errorMessage));

        /// <summary>
        /// Expect a response with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrors<TSelf>(this IHttpResponseExpectations<TSelf> tester, params string[] messages) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="errors"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrors<TSelf>(this IHttpResponseExpectations<TSelf> tester, params ApiError[] errors) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectMessages(errors));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectMessages<TSelf>(this IHttpResponseExpectations<TSelf> tester, params string[] messages) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IHttpResponseExpectations{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectMessages<TSelf>(this IHttpResponseExpectations<TSelf> tester, MessageItemCollection messages) where TSelf : IHttpResponseExpectations<TSelf>
            => tester.SetHttpResponseExpectation(t => t.HttpResponseExpectations.SetExpectMessages(messages));

        #endregion

        #region IResponseValueExpectations<TValue, TSelf>

        /// <summary>
        /// Invokes the set expectation logic.
        /// </summary>
        private static TSelf SetResponseValueExpectation<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, Action<IResponseValueExpectations<TValue, TSelf>> action) where TSelf : IResponseValueExpectations<TValue, TSelf>
        {
            action(tester);
            return (TSelf)tester;
        }

        /// <summary>
        /// Expect a <c>null</c> response value.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectNullValue<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectNullValue());

        /// <summary>
        /// Expects the <see cref="IPrimaryKey"/> to be implemented and have non-null <see cref="CompositeKey.Args"/>.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectPrimaryKey<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectPrimaryKey());

        /// <summary>
        /// Expects the <see cref="IETag"/> to be implemaned and have a non-null value and different to <paramref name="previousETag"/> where specified.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="previousETag">The previous ETag value.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectETag<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, string? previousETag = null) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectETag(previousETag));

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.CreatedBy"/> and <see cref="ChangeLog.CreatedDate"/> matching the specified values.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="createdby">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="createdDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf SetExpectedChangeLogCreated<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, string? createdby = null, DateTime? createdDateGreaterThan = null) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectedChangeLogCreated(createdby, createdDateGreaterThan));

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.UpdatedBy"/> and <see cref="ChangeLog.UpdatedDate"/> matching the specified values.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="updatedBy">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="updatedDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf SetExpectedChangeLogUpdated<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, string? updatedBy = null, DateTime? updatedDateGreaterThan = null) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectedChangeLogUpdated(updatedBy, updatedDateGreaterThan));

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValueFunc"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="expectedValueFunc">The function to generate the response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectedValue<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, Func<TSelf, TValue> expectedValueFunc, params string[] membersToIgnore) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectedValue(tester, t => expectedValueFunc((TSelf)t), membersToIgnore));

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValue"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="expectedValue">The expected response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectedValue<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester, TValue expectedValue, params string[] membersToIgnore) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.SetExpectedValue(tester, _ => expectedValue ?? throw new ArgumentNullException(nameof(expectedValue)), membersToIgnore));

        /// <summary>
        /// Ignores the <see cref="IChangeLog.ChangeLog"/> property.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreChangeLog<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.MembersToIgnore.Add(nameof(IChangeLog.ChangeLog)));

        /// <summary>
        /// Ignores the <see cref="IETag.ETag"/> property.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreETag<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.MembersToIgnore.Add(nameof(IETag.ETag)));

        /// <summary>
        /// Ignores the <see cref="IPrimaryKey.PrimaryKey"/> property.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnorePrimaryKey<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.MembersToIgnore.Add(nameof(IETag.ETag)));

        /// <summary>
        /// Ignores the <see cref="IIdentifier.Id"/> property.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="IResponseValueExpectations{TValue, TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreId<TValue, TSelf>(this IResponseValueExpectations<TValue, TSelf> tester) where TSelf : IResponseValueExpectations<TValue, TSelf>
            => tester.SetResponseValueExpectation(t => t.ResponseValueExpectations.MembersToIgnore.Add(nameof(IIdentifier.Id)));

        #endregion

        #region IEventExpectations<TSelf>

        /// <summary>
        /// Invokes the set expectation logic.
        /// </summary>
        private static TSelf SetEventExpectation<TSelf>(this IEventExpectations<TSelf> tester, Action<IEventExpectations<TSelf>> action) where TSelf : IEventExpectations<TSelf>
        {
            action(tester);
            return (TSelf)tester;
        }

        /// <summary>
        /// Expects that the corresponding event has been published (in order specified). The expected event <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> properties are not matched/verified.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>On first invocation will automatically replace <see cref="IEventPublisher"/> with a new <see cref="InMemoryPublisher"/> scoped service (DI) to capture events for this expectation. The other services are therefore required
        /// for this to function. As this is a scoped service no parallel execution of services against the same test host is supported as this capability is not considered thread-safe.</remarks>
        public static TSelf ExpectEvent<TSelf>(this IEventExpectations<TSelf> tester, string subject, string? action = "*") where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(null, subject, action));

        /// <summary>
        /// Expects that the corresponding event has been published (in order specified). The expected event <paramref name="source"/>, <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> 
        /// properties are not matched/verified.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>On first invocation will automatically replace <see cref="IEventPublisher"/> with a new <see cref="InMemoryPublisher"/> scoped service (DI) to capture events for this expectation. The other services are therefore required
        /// for this to function. As this is a scoped service no parallel execution of services against the same test host is supported as this capability is not considered thread-safe.</remarks>
        public static TSelf ExpectEvent<TSelf>(this IEventExpectations<TSelf> tester, string source, string subject, string? action = "*") where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(null, source, subject, action));

        /// <summary>
        /// Expects that the corresponding <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public static TSelf ExpectEvent<TSelf>(this IEventExpectations<TSelf> tester, EventData @event, params string[] membersToIgnore) where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(null, @event, membersToIgnore));

        /// <summary>
        /// Expects that the corresponding <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public static TSelf ExpectEvent<TSelf>(this IEventExpectations<TSelf> tester, string source, EventData @event, params string[] membersToIgnore) where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(null, source, @event, membersToIgnore));

        /// <summary>
        /// Expects that the corresponding <paramref name="destination"/> event has been published (in order specified). The expected event <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> properties are not matched/verified.
        /// </summary>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>On first invocation will automatically replace <see cref="IEventPublisher"/> with a new <see cref="InMemoryPublisher"/> scoped service (DI) to capture events for this expectation. The other services are therefore required
        /// for this to function. As this is a scoped service no parallel execution of services against the same test host is supported as this capability is not considered thread-safe.</remarks>
        public static TSelf ExpectDestinationEvent<TSelf>(this IEventExpectations<TSelf> tester, string destination, string subject, string? action = "*") where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(destination, subject, action));

        /// <summary>
        /// Expects that the corresponding <paramref name="destination"/> event has been published (in order specified). The expected event <paramref name="source"/>, <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> 
        /// properties are not matched/verified.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>On first invocation will automatically replace <see cref="IEventPublisher"/> with a new <see cref="InMemoryPublisher"/> scoped service (DI) to capture events for this expectation. The other services are therefore required
        /// for this to function. As this is a scoped service no parallel execution of services against the same test host is supported as this capability is not considered thread-safe.</remarks>
        public static TSelf ExpectDestinationEvent<TSelf>(this IEventExpectations<TSelf> tester, string destination, string source, string subject, string? action = "*") where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(destination, source, subject, action));

        /// <summary>
        /// Expects that the corresponding <paramref name="destination"/> <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public static TSelf ExpectDestinationEvent<TSelf>(this IEventExpectations<TSelf> tester, string destination, EventData @event, params string[] membersToIgnore) where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(destination, @event, membersToIgnore));

        /// <summary>
        /// Expects that the corresponding <paramref name="destination"/> <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="tester">The <see cref="IEventExpectations{TSelf}"/>.</param>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public static TSelf ExpectDestinationEvent<TSelf>(this IEventExpectations<TSelf> tester, string destination, string source, EventData @event, params string[] membersToIgnore) where TSelf : IEventExpectations<TSelf>
            => tester.SetEventExpectation(t => t.EventExpectations.Expect(destination, source, @event, membersToIgnore));

        #endregion

        #region IExceptionSuccessExpectations<TSelf>

        /// <summary>
        /// Expects that an <see cref="Exception"/> will be thrown during execution.
        /// </summary>
        /// <param name="tester">The <see cref="IExceptionSuccessExpectations{TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static ExceptionExpectation<TSelf> ExpectException<TSelf>(this IExceptionSuccessExpectations<TSelf> tester) where TSelf : IExceptionSuccessExpectations<TSelf> => new(tester);

        /// <summary>
        /// Expects that the execution was successful; i.e. there was no <see cref="Exception"/> thrown.
        /// </summary>
        /// <param name="tester">The <see cref="IExceptionSuccessExpectations{TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectSuccess<TSelf>(this IExceptionSuccessExpectations<TSelf> tester) where TSelf : IExceptionSuccessExpectations<TSelf>
        { 
            tester.ExceptionSuccessExpectations.ExpectSuccess();
            return (TSelf)tester;
        }

        /// <summary>
        /// Provides <see cref="Any"/> and <see cref="Type"/> <see cref="Exception"/> expectations.
        /// </summary>
        public struct ExceptionExpectation<TSelf> where TSelf : IExceptionSuccessExpectations<TSelf>
        {
            private readonly IExceptionSuccessExpectations<TSelf> _tester;

            /// <summary>
            /// Initializes a new instance of the <see cref="ExceptionExpectation{TSelf}"/> struct.
            /// </summary>
            /// <param name="tester">The <see cref="IExceptionSuccessExpectations{TSelf}"/>.</param>
            internal ExceptionExpectation(IExceptionSuccessExpectations<TSelf> tester) => _tester = tester;

            /// <summary>
            /// Expects that an <see cref="Exception"/> of any <see cref="Type"/> will be thrown during execution.
            /// </summary>
            /// <param name="expectedMessage">The optional expected message to match.</param>
            /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
            public TSelf Any(string? expectedMessage = null)
            {
                _tester.ExceptionSuccessExpectations.ExpectException(expectedMessage);
                return (TSelf)_tester;
            }

            /// <summary>
            /// Expects that an <see cref="Exception"/> of <see cref="Type"/> <typeparamref name="TException"/> will be thrown during execution.
            /// </summary>
            /// <typeparam name="TException">The expected <see cref="Exception"/> <see cref="Type"/>.</typeparam>
            /// <param name="expectedMessage">The optional expected message to match.</param>
            /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
            public TSelf Type<TException>(string? expectedMessage = null) where TException : Exception
            {
                _tester.ExceptionSuccessExpectations.ExpectException<TException>(expectedMessage);
                return (TSelf)_tester;
            }
        }

        #endregion
    }
}