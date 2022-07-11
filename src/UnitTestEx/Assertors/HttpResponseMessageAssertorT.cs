// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper with a specified response <typeparamref name="TValue"/> <see cref="Type"/>.
    /// </summary>
    public class HttpResponseMessageAssertor<TValue> : HttpResponseMessageAssertorBase<HttpResponseMessageAssertor<TValue>>
    {
        private TValue? _value;
        private bool _valueIsDeserialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertor(HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer) : base(response, implementor, jsonSerializer) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="value">The value already deserialized.</param>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertor(TValue value, HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer) : base(response, implementor, jsonSerializer)
        {
            _value = value;
            _valueIsDeserialized = true;
        }

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
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedUri"/> result.
        /// </summary>
        /// <param name="expectedUri">The expected <see cref="Uri"/> function.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertLocationHeader(Func<TValue?, Uri> expectedUri)
        {
            Implementor.AssertAreEqual(expectedUri?.Invoke(GetValue<TValue>()), Response.Headers?.Location, $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> Assert(TValue? expectedValue, params string[] membersToIgnore)
        {
            var cr = ObjectComparer.Compare(expectedValue, Value, membersToIgnore);
            if (!cr.AreEqual)
                Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TValue>(resourceName, Assembly.GetCallingAssembly(), JsonSerializer), membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor<TValue> AssertFromJsonResource<TAssembly>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TValue>(resourceName, typeof(TAssembly).Assembly, JsonSerializer), membersToIgnore);
    }
}