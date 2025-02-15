// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Net.Http.Headers;
using System;
using System.Net.Http;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper with a specified response <typeparamref name="TValue"/> <see cref="Type"/>.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    public class HttpResponseMessageAssertor<TValue>(TesterBase owner, HttpResponseMessage response) : HttpResponseMessageAssertorBase<HttpResponseMessageAssertor<TValue>>(owner, response)
    {
        private TValue? _value;
        private bool _valueIsDeserialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="value">The value already deserialized.</param>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        public HttpResponseMessageAssertor(TesterBase owner, TValue value, HttpResponseMessage response) : this(owner, response)
        {
            _value = value;
            _valueIsDeserialized = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="assertor">The untyped <see cref="HttpResponseMessageAssertor"/>.</param>
        internal HttpResponseMessageAssertor(HttpResponseMessageAssertor assertor) : this(assertor.Owner, assertor.Response) { }

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <returns>The result value.</returns>
        public TValue? Value
        {
            get
            {
                if (!_valueIsDeserialized)
                {
                    _value = GetValue<TValue>();
                    _valueIsDeserialized = true;
                }

                return _value;
            }
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expected"/> string.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertLocationHeader(Func<TValue?, string> expected)
        {
            Implementor.AssertAreEqual(expected?.Invoke(GetValue<TValue>()), Response.Headers?.Location?.ToString(), $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedUri"/> result.
        /// </summary>
        /// <param name="expectedUri">The expected <see cref="Uri"/> function.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertLocationHeader(Func<TValue?, Uri> expectedUri)
        {
            Implementor.AssertAreEqual(expectedUri?.Invoke(GetValue<TValue>()), Response.Headers?.Location, $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

        /// <summary>
        /// Asserts the the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> contains the <paramref name="expected"/> string.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertLocationHeaderContains(string expected)
        {
            if (string.IsNullOrEmpty(expected))
                throw new ArgumentNullException(nameof(expected));

            if (Response.Headers?.Location == null)
                Implementor.AssertFail($"The Actual HTTP Response Header '{HeaderNames.Location}' must not be null.");

            if (!Response.Headers!.Location!.ToString().Contains(expected))
                Implementor.AssertFail($"Actual HTTP Response Header '{HeaderNames.Location}' must contain expected.");

            return this;
        }

        /// <summary>
        /// Asserts the the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> contains the <paramref name="expected"/> string result.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertLocationHeaderContains(Func<TValue?, string> expected)
            => AssertLocationHeaderContains(expected?.Invoke(GetValue<TValue>())!);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertValue(TValue? expectedValue, params string[] pathsToIgnore)
        {
            var cr = Owner.CreateJsonComparer().CompareValue(expectedValue, Value, pathsToIgnore);
            if (cr.HasDifferences)
                Implementor.AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");

            return this;
        }
    }
}