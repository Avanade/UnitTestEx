// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Json;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper.
    /// </summary>
    public class HttpResponseMessageAssertor : HttpResponseMessageAssertorBase<HttpResponseMessageAssertor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertor(HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer) : base(response, implementor, jsonSerializer) { }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedUri"/> result.
        /// </summary>
        /// <param name="expectedUri">The expected <see cref="Uri"/> function.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertLocationHeader<TValue>(Func<TValue?, Uri> expectedUri)
        {
            Implementor.AssertAreEqual(expectedUri?.Invoke(GetValue<TValue>()), Response.Headers?.Location, $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor Assert<TValue>(TValue? expectedValue, params string[] membersToIgnore)
        {
            if (Response.Content == null)
            {
                Implementor.AssertAreEqual(expectedValue, default, "Expected and Actual (no content) values are not equal");
                return this;
            }

            if (Response.Content.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                var json = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (expectedValue == null)
                {
                    if (!string.IsNullOrEmpty(json))
                        Implementor.AssertFail($"Expected null and actual has content: {json}");

                    return this;
                }

                var val = JsonSerializer.Deserialize<TValue>(json);
                var cr = ObjectComparer.Compare(expectedValue, val, membersToIgnore);
                if (!cr.AreEqual)
                    Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }
            else
                Implementor.AssertAreEqual(expectedValue?.ToString(), Response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), "Expected and Actual values are not equal.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertFromJsonResource<TValue>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TValue>(resourceName, Assembly.GetCallingAssembly(), JsonSerializer), membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertFromJsonResource<TAssembly, TValue>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TValue>(resourceName, typeof(TAssembly).Assembly, JsonSerializer), membersToIgnore);

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <typeparam name="TValue">The resulting value <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        public new TValue? GetValue<TValue>() => base.GetValue<TValue>();
    }
}