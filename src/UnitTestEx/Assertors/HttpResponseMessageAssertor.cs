// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Net.Http.Headers;
using System;
using System.Net.Http;
using System.Net.Mime;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    public class HttpResponseMessageAssertor(TesterBase owner, HttpResponseMessage response) : HttpResponseMessageAssertorBase<HttpResponseMessageAssertor>(owner, response)
    {
        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedUri"/> result.
        /// </summary>
        /// <param name="expectedUri">The expected <see cref="Uri"/> function.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertLocationHeader<TValue>(Func<TValue?, Uri> expectedUri)
        {
            Implementor.AssertAreEqual(expectedUri?.Invoke(GetValue<TValue>()), Response.Headers?.Location, $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertValue<TValue>(TValue? expectedValue, params string[] pathsToIgnore)
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
                var cr = Owner.CreateJsonComparer().CompareValue(expectedValue, val, pathsToIgnore);
                if (cr.HasDifferences)
                    Implementor.AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
            }
            else
                Implementor.AssertAreEqual(expectedValue?.ToString(), Response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), "Expected and Actual values are not equal.");

            return this;
        }
    }
}