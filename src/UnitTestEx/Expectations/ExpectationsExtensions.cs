// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Abstractions;
using CoreEx.Entities;
using System;
using System.Linq;
using System.Net;
using System.Text;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="HttpTestExpectations"/> functionality for an <see cref="HttpTesterBase{TSelf}"/>.
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

        #region HttpTesterBase<TSelf>

        /// <summary>
        /// Invokes the set expectation logic.
        /// </summary>
        private static TSelf SetExpectation<TSelf>(this HttpTesterBase<TSelf> tester, Action<HttpTesterBase<TSelf>> action) where TSelf : HttpTesterBase<TSelf>
        {
            action(tester);
            return (TSelf)tester;
        }

        /// <summary>
        /// Expects a response with the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectStatusCode<TSelf>(this HttpTesterBase<TSelf> tester, HttpStatusCode statusCode = HttpStatusCode.OK) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectStatusCode(statusCode));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorType"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errorType">The <see cref="ErrorType"/>.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TSelf>(this HttpTesterBase<TSelf> tester, ErrorType errorType, string? errorMessage = null) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectErrorType((int)errorType, errorMessage));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorCode"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TSelf>(this HttpTesterBase<TSelf> tester, int errorCode, string? errorMessage = null) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectErrorType(errorCode, errorMessage));

        /// <summary>
        /// Expect a response with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        public static TSelf ExpectErrors<TSelf>(this HttpTesterBase<TSelf> tester, params string[] messages) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="errors"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <remarks>Will only check the <see cref="ApiError.Field"/> where specified (not <c>null</c>).</remarks>
        public static TSelf ExpectErrors<TSelf>(this HttpTesterBase<TSelf> tester, params ApiError[] errors) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(errors));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <remarks>Will only check the <see cref="MessageItem.Property"/> where specified (not <c>null</c>).</remarks>
        public static TSelf ExpectMessages<TSelf>(this HttpTesterBase<TSelf> tester, params string[] messages) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <remarks>Will only check the <see cref="MessageItem.Property"/> where specified (not <c>null</c>).</remarks>
        public static TSelf ExpectMessages<TSelf>(this HttpTesterBase<TSelf> tester, MessageItemCollection messages) where TSelf : HttpTesterBase<TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        #endregion

        #region HttpTesterBase<TResponse, TSelf>

        /// <summary>
        /// Invokes the set expectation logic.
        /// </summary>
        private static TSelf SetExpectation<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, Action<HttpTesterBase<TResponse, TSelf>> action) where TSelf : HttpTesterBase<TResponse, TSelf>
        {
            action(tester);
            return (TSelf)tester;
        }

        /// <summary>
        /// Expects a response with the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectStatusCode<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, HttpStatusCode statusCode = HttpStatusCode.OK) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectStatusCode(statusCode));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorType"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errorType">The <see cref="ErrorType"/>.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, ErrorType errorType, string? errorMessage = null) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectErrorType((int)errorType, errorMessage));

        /// <summary>
        /// Expects a response with the specified <paramref name="errorCode"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message will not be checked.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrorType<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, int errorCode, string? errorMessage = null) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectErrorType(errorCode, errorMessage));

        /// <summary>
        /// Expect a response with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrors<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, params string[] messages) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="errors"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <remarks>Will only check the <see cref="ApiError.Field"/> where specified (not <c>null</c>).</remarks>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectErrors<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, params ApiError[] errors) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(errors));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <remarks>Will only check the <see cref="MessageItem.Property"/> where specified (not <c>null</c>).</remarks>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectMessages<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, params string[] messages) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <remarks>Will only check the <see cref="MessageItem.Property"/> where specified (not <c>null</c>).</remarks>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectMessages<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, MessageItemCollection messages) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectMessages(messages));

        /// <summary>
        /// Expect a <c>null</c> response value.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectNullValue<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectNullValue());

        /// <summary>
        /// Expects the <see cref="IPrimaryKey"/> to be implemented and have non-null <see cref="CompositeKey.Args"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectPrimaryKey<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectPrimaryKey());

        /// <summary>
        /// Expects the <see cref="IETag"/> to be implemaned and have a non-null value and different to <paramref name="previousETag"/> where specified.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="previousETag">The previous ETag value.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectETag<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, string? previousETag = null) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectETag(previousETag));

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.CreatedBy"/> and <see cref="ChangeLog.CreatedDate"/> matching the specified values.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="createdby">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="createdDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf SetExpectedChangeLogCreated<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, string? createdby = null, DateTime? createdDateGreaterThan = null) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectedChangeLogCreated(createdby, createdDateGreaterThan));

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.UpdatedBy"/> and <see cref="ChangeLog.UpdatedDate"/> matching the specified values.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="updatedBy">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="updatedDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf SetExpectedChangeLogUpdated<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, string? updatedBy = null, DateTime? updatedDateGreaterThan = null) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectedChangeLogUpdated(updatedBy, updatedDateGreaterThan));

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValueFunc"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="expectedValueFunc">The function to generate the response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectedValue<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, Func<TSelf, TResponse>? expectedValueFunc, params string[] membersToIgnore) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectedValue(expectedValueFunc, membersToIgnore));

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValue"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <param name="expectedValue">The expected response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectedValue<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester, TResponse expectedValue, params string[] membersToIgnore) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.SetExpectedValue(_ => expectedValue ?? throw new ArgumentNullException(nameof(expectedValue)), membersToIgnore));

        /// <summary>
        /// Ignores the <see cref="IChangeLog.ChangeLog"/> property.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreChangeLog<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.MembersToIgnore.Add(nameof(IChangeLog.ChangeLog)));

        /// <summary>
        /// Ignores the <see cref="IETag.ETag"/> property.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreETag<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.MembersToIgnore.Add(nameof(IETag.ETag)));

        /// <summary>
        /// Ignores the <see cref="IPrimaryKey.PrimaryKey"/> property.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnorePrimaryKey<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.MembersToIgnore.Add(nameof(IETag.ETag)));

        /// <summary>
        /// Ignores the <see cref="IIdentifier.Id"/> property.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TSelf">The tester <see cref="Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTesterBase{TSelf}"/>.</param>
        /// <returns>The <see cref="HttpTesterBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf IgnoreId<TResponse, TSelf>(this HttpTesterBase<TResponse, TSelf> tester) where TSelf : HttpTesterBase<TResponse, TSelf>
            => tester.SetExpectation(t => t.Expectations.MembersToIgnore.Add(nameof(IIdentifier.Id)));

        #endregion
    }
}