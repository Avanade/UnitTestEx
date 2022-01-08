// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper.
    /// </summary>
    public class HttpResponseMessageAssertor
    {
        private readonly TestFrameworkImplementor _implementor;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal HttpResponseMessageAssertor(HttpResponseMessage response, TestFrameworkImplementor implementor)
        {
            Response = response;
            _implementor = implementor;
        }

        /// <summary>
        /// Gets the <see cref="HttpResponseMessage"/>.
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessage.StatusCode"/> has a successful value between 200 and 299.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertSuccessStatusCode()
        {
            var sc = (int)Response.StatusCode;
            if (sc < 200 || sc > 299)
                _implementor.AssertFail($"Result Status Code '{sc}' must be between 200 and 299 to be considered successful.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> has the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor Assert(HttpStatusCode statusCode)
        {
            _implementor.AssertAreEqual(statusCode, Response.StatusCode);
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> has the specified <paramref name="statusCode"/> and <paramref name="content"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="content">The expected content.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor Assert(HttpStatusCode statusCode, string? content)
        {
            Assert(statusCode);
            _implementor.AssertAreEqual(content, Response.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> has the specified <paramref name="statusCode"/> and <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        private HttpResponseMessageAssertor Assert(object? expectedValue, HttpStatusCode statusCode, params string[] membersToIgnore)
        {
            Assert(statusCode);
            AssertSuccessStatusCode();

            if (Response.Content.Headers.ContentType.MediaType == MediaTypeNames.Application.Json)
            {
                var json = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (expectedValue == null)
                {
                    if (!string.IsNullOrEmpty(json))
                        _implementor.AssertFail($"Expected null and actual has content: {json}");

                    return this;
                }

                if (expectedValue is JToken jte)
                {
                    var jta = JToken.Parse(json);
                    if (!JToken.DeepEquals(jte, jta))
                        _implementor.AssertFail($"Expected and Actual JSON are not equal: {Environment.NewLine}Expected =>{Environment.NewLine}{jte?.ToString(Formatting.Indented)}{Environment.NewLine}Actual =>{Environment.NewLine}{jta?.ToString(Formatting.Indented)}");

                    return this;
                }

                var val = JsonConvert.DeserializeObject(json, expectedValue.GetType());
                var cr = ObjectComparer.Compare(expectedValue, val, membersToIgnore);
                if (!cr.AreEqual)
                    _implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }
            else
                _implementor.AssertAreEqual(expectedValue?.ToString(), Response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertNotFound() => Assert(HttpStatusCode.NotFound);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertNoContent() => Assert(HttpStatusCode.NoContent);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.OK"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertOK() => Assert(HttpStatusCode.OK);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NotModified"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertNotModified() => Assert(HttpStatusCode.NotModified);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.OK"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertOK(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.OK"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertOKFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.OK"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertOKFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertCreated() => Assert(HttpStatusCode.Created);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertCreated(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.Created, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertCreatedFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Created, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertCreatedFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Created, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertAccepted() => Assert(HttpStatusCode.Accepted);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertAccepted(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertAcceptedFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertAcceptedFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.BadRequest"/> and contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public HttpResponseMessageAssertor AssertBadRequest(params string[] messages)
        {
            var expected = new List<ApiError>();
            foreach (var m in messages)
            {
                expected.Add(new ApiError(null, m));
            }

            AssertBadRequestErrors(expected, false);
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.BadRequest"/> and contains the <paramref name="expected"/> errors.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertBadRequest(params ApiError[] expected)
        {
            AssertBadRequestErrors(expected, true);
            return this;
        }

        /// <summary>
        /// Asserts the expected vs actual errors.
        /// </summary>
        private void AssertBadRequestErrors(IEnumerable<ApiError> expected, bool includeField)
        {
            var val = GetValue<Dictionary<string, string[]>>();
            var act = new List<ApiError>();
            foreach (var err in val)
            {
                foreach (var msg in err.Value)
                {
                    act.Add(new ApiError(includeField ? err.Key : null, msg));
                }
            }

            if (!ApiError.TryAreMatched(expected, act, includeField, out var message))
                _implementor.AssertFail(message);
        }

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        public TResult GetValue<TResult>()
        {
            _implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            return JsonConvert.DeserializeObject<TResult>(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        }

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <returns>The result value.</returns>
        public object? GetValue()
        {
            _implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            return JsonConvert.DeserializeObject(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        }
    }
}