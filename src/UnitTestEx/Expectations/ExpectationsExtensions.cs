// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Reflection;
using UnitTestEx.Json;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables the <b>Expectations</b> extension methods.
    /// </summary>
    public static class ExpectationsExtensions
    {
        #region ExceptionExpectations

        /// <summary>
        /// Expects that the execution was successful; i.e. there was no <see cref="Exception"/> thrown.
        /// </summary>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectSuccess<TSelf>(this IExpectations<TSelf> expectations) where TSelf : IExpectations<TSelf>
        {
            expectations.ExpectationsArranger.GetOrAdd(() => new ExceptionExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).ExpectSuccess = true;
            return (TSelf)expectations;
        }

        /// <summary>
        /// Expects that an <see cref="Exception"/> will be thrown during execution as further configured by the resulting <see cref="ExceptionExpectation{TSelf}"/>.
        /// </summary>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static ExceptionExpectation<TSelf> ExpectException<TSelf>(this IExpectations<TSelf> expectations) where TSelf : IExpectations<TSelf> => new(expectations);

        /// <summary>
        /// Expects that any <see cref="Exception"/> will be thrown during execution with the <paramref name="expectedMessage"/> to match (contains).
        /// </summary>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <param name="expectedMessage">The optional expected message to match (contains).</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public static TSelf ExpectException<TSelf>(this IExpectations<TSelf> expectations, string? expectedMessage) where TSelf : IExpectations<TSelf> => new ExceptionExpectation<TSelf>(expectations).Any(expectedMessage);

        /// <summary>
        /// Provides <see cref="Any"/> and <see cref="Type"/> <see cref="Exception"/> expectations.
        /// </summary>
        public class ExceptionExpectation<TSelf> where TSelf : IExpectations<TSelf>
        {
            private readonly IExpectations<TSelf> _expectations;

            /// <summary>
            /// Initializes a new instance of the <see cref="ExceptionExpectation{TSelf}"/> struct.
            /// </summary>
            /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
            internal ExceptionExpectation(IExpectations<TSelf> expectations) => _expectations = expectations;

            /// <summary>
            /// Expects that an <see cref="Exception"/> of any <see cref="Type"/> will be thrown during execution.
            /// </summary>
            /// <param name="expectedMessage">The optional expected message to match (contains).</param>
            /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
            public TSelf Any(string? expectedMessage = null)
            {
                _expectations.ExpectationsArranger.GetOrAdd(() => new ExceptionExpectations<TSelf>(_expectations.ExpectationsArranger.Owner, (TSelf)_expectations)).SetExpectException(null, expectedMessage);
                return (TSelf)_expectations;
            }

            /// <summary>
            /// Expects that an <see cref="Exception"/> of <see cref="Type"/> <typeparamref name="TException"/> will be thrown during execution.
            /// </summary>
            /// <typeparam name="TException">The expected <see cref="Exception"/> <see cref="Type"/>.</typeparam>
            /// <param name="expectedMessage">The optional expected message to match.</param>
            /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
            public TSelf Type<TException>(string? expectedMessage = null) where TException : Exception
            {
                _expectations.ExpectationsArranger.GetOrAdd(() => new ExceptionExpectations<TSelf>(_expectations.ExpectationsArranger.Owner, (TSelf)_expectations)).SetExpectException(typeof(TException), expectedMessage);
                return (TSelf)_expectations;
            }
        }

        #endregion

        #region ErrorExpectations

        /// <summary>
        /// Expects that an error will be returned matching (contains) the specified <paramref name="message"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <param name="message">The error message.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectError<TSelf>(this IExpectations<TSelf> expectations, string message) where TSelf : IExpectations<TSelf>
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            expectations.ExpectationsArranger.GetOrAdd(() => new ErrorExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).Error = message;
            return (TSelf)expectations;
        }

        /// <summary>
        /// Expects that one or more errors will be returned matching the specified <paramref name="messages"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <param name="messages">The error messages.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectErrors<TSelf>(this IExpectations<TSelf> expectations, params string[] messages) where TSelf : IExpectations<TSelf>
        {
            if (messages.Length == 0)
                throw new ArgumentException("At least one message must be specified.", nameof(messages));

            var errors = new ApiError[messages.Length];
            for (var i = 0; i < messages.Length; i++)
                errors[i] = new ApiError(null, messages[i]);

            return ExpectErrors(expectations, errors);
        }

        /// <summary>
        /// Expects that one or more errors will be returned matching the specified <paramref name="errors"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <param name="errors">The errors.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectErrors<TSelf>(this IExpectations<TSelf> expectations, params ApiError[] errors) where TSelf : IExpectations<TSelf>
        {
            if (errors.Length == 0)
                throw new ArgumentException("At least one error must be specified.", nameof(errors));

            expectations.ExpectationsArranger.GetOrAdd(() => new ErrorExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).Errors.AddRange(errors);
            return (TSelf)expectations;
        }

        #endregion

        #region LoggerExpectations

        /// <summary>
        /// Expects that the <see cref="ILogger"/> will have logged a message that contains the specified <paramref name="texts"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IExpectations{TSelf}"/>.</param>
        /// <param name="texts">The text(s) that should appear in at least one log message.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectLogContains<TSelf>(this IExpectations<TSelf> expectations, params string[] texts) where TSelf : IExpectations<TSelf>
        {
            expectations.ExpectationsArranger.GetOrAdd(() => new LoggerExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).SetExpectLogContains(texts);
            return (TSelf)expectations;
        }

        #endregion

        #region HttpResponseMessageExpectations

        /// <summary>
        /// Expects that the <see cref="HttpResponseMessage"/> will have the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IHttpResponseMessageExpectations{TSelf}"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectStatusCode<TSelf>(this IHttpResponseMessageExpectations<TSelf> expectations, HttpStatusCode statusCode = HttpStatusCode.OK) where TSelf : IHttpResponseMessageExpectations<TSelf>
        {
            expectations.ExpectationsArranger.GetOrAdd(() => new HttpResponseMessageExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).SetExpectStatusCode(statusCode);
            return (TSelf)expectations;
        }

        #endregion

        #region ValueExpectations

        /// <summary>
        /// Expects that the value will be equal (uses <see cref="JsonElementComparer"/>) to the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="value">The value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectValue<TSelf, TValue>(this IValueExpectations<TValue, TSelf> expectations, TValue value, params string[] pathsToIgnore) where TSelf : IValueExpectations<TValue, TSelf>
            => ExpectValue(expectations, _ => value, pathsToIgnore);

        /// <summary>
        /// Expects that the value will be equal (uses <see cref="JsonElementComparer"/>) to the specified resulting <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="value">The value function.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
        public static TSelf ExpectValue<TSelf, TValue>(this IValueExpectations<TValue, TSelf> expectations, Func<TSelf, TValue?> value, params string[] pathsToIgnore) where TSelf : IValueExpectations<TValue, TSelf>
        {
            expectations.ExpectationsArranger.GetOrAdd(() => new ValueExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).SetExpectJson(t => expectations.ExpectationsArranger.Owner.JsonSerializer.Serialize(value(t)));
            expectations.ExpectationsArranger.PathsToIgnore.AddRange(pathsToIgnore);
            return (TSelf)expectations;
        }

        /// <summary>
        /// Expects that the resultant value will be equal (uses <see cref="JsonElementComparer"/>) to the specified <paramref name="json"/>.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns> 
#if NET7_0_OR_GREATER
        public static TSelf ExpectJson<TSelf, TValue>(this IValueExpectations<TValue, TSelf> expectations, [StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore) where TSelf : IValueExpectations<TValue, TSelf>
#else
        public static TSelf ExpectJson<TSelf, TValue>(this IValueExpectations<TValue, TSelf> expectations, string json, params string[] pathsToIgnore) where TSelf : IValueExpectations<TValue, TSelf>
#endif
        {
            expectations.ExpectationsArranger.GetOrAdd(() => new ValueExpectations<TSelf>(expectations.ExpectationsArranger.Owner, (TSelf)expectations)).SetExpectJson(t => json);
            expectations.ExpectationsArranger.PathsToIgnore.AddRange(pathsToIgnore);
            return (TSelf)expectations;
        }

        /// <summary>
        /// Expects that the resultant value will be equal (uses <see cref="JsonElementComparer"/>) to the JSON from the named embedded resource.
        /// </summary>
        /// <typeparam name="TSelf">The expectations <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectations">The <see cref="IValueExpectations{TValue, TSelf}"/>.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>Uses <see cref="Resource.GetJson(string, Assembly?)"/> to load the embedded resource within the <see cref="Assembly.GetCallingAssembly"/>.</remarks>
        public static TSelf ExpectJsonFromResource<TSelf, TValue>(this IValueExpectations<TValue, TSelf> expectations, string resourceName, params string[] pathsToIgnore) where TSelf : IValueExpectations<TValue, TSelf>
            => ExpectJson(expectations, Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), pathsToIgnore);

#endregion
    }
}