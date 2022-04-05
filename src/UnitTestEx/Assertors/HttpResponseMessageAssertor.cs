// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper.
    /// </summary>
    public class HttpResponseMessageAssertor
    {
        private readonly TestFrameworkImplementor _implementor;
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertor"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertor(HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer)
        {
            Response = response;
            _implementor = implementor;
            _jsonSerializer = jsonSerializer;
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
        /// Asserts that the <see cref="Response"/> content type is <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertContentTypeJson() => AssertContentType(MediaTypeNames.Application.Json);

        /// <summary>
        /// Asserts that the <see cref="Response"/> content type is <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertContentTypePlainText() => AssertContentType(MediaTypeNames.Text.Plain);

        /// <summary>
        /// Asserts that the <see cref="Response"/> content type matches the <paramref name="expectedContentType"/>.
        /// </summary>
        /// <param name="expectedContentType">The expected content type.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertContentType(string expectedContentType)
        {
            _implementor.AssertAreEqual(expectedContentType, Response?.Content?.Headers?.ContentType?.MediaType);
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
        /// Asserts that the <see cref="Response"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor Assert<TResult>(TResult expectedValue, params string[] membersToIgnore)
        {
            if (Response.Content == null)
            {
                _implementor.AssertAreEqual(expectedValue?.ToString(), null, "Expected and Actual (no content) values are not equal");
                return this;
            }

            if (Response.Content.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                var json = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (expectedValue == null)
                {
                    if (!string.IsNullOrEmpty(json))
                        _implementor.AssertFail($"Expected null and actual has content: {json}");

                    return this;
                }

                var val = _jsonSerializer.Deserialize<TResult>(json);
                var cr = ObjectComparer.Compare(expectedValue, val, membersToIgnore);
                if (!cr.AreEqual)
                    _implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }
            else
                _implementor.AssertAreEqual(expectedValue?.ToString(), Response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), "Expected and Actual values are not equal.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly(), _jsonSerializer), membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertFromJsonResource<TAssembly, TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, typeof(TAssembly).Assembly, _jsonSerializer), membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Response"/> JSON content matches the specified <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The expected JSON.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            if (Response.Content == null)
            {
                _implementor.AssertAreEqual(json, null, "Expected and Actual (no content) JSON values are not equal");
                return this;
            }

            if (Response.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                var exp = new Utf8JsonReader(new BinaryData(json));
                if (!JsonElement.TryParseValue(ref exp, out JsonElement? eje))
                    throw new ArgumentException("Expected JSON is not considered valid.", nameof(json));

                var act = new Utf8JsonReader(new BinaryData(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                if (!JsonElement.TryParseValue(ref act, out JsonElement? aje))
                    _implementor.AssertFail("Actual value is not considered valid JSON.");

                if (!new JsonElementComparer().Equals((JsonElement)eje!, (JsonElement)aje!))
                    _implementor.AssertFail("Expected and Actual JSON values are not equal.");
            }
            else
                _implementor.AssertAreEqual(json, Response.Content?.ReadAsStringAsync().GetAwaiter().GetResult(), "Expected and Actual JSON values are not equal.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertCreated() => Assert(HttpStatusCode.Created);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertAccepted() => Assert(HttpStatusCode.Accepted);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.PreconditionFailed"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertPreconditionFailed() => Assert(HttpStatusCode.PreconditionFailed);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Conflict"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertConflict() => Assert(HttpStatusCode.Conflict);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Unauthorized"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertUnauthorized() => Assert(HttpStatusCode.Unauthorized);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Forbidden"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertForbidden() => Assert(HttpStatusCode.Forbidden);

        /// <summary>
        /// Asserts that the <see cref="Response"/> contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public HttpResponseMessageAssertor AssertErrors(params string[] messages)
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
        /// Asserts that the <see cref="Response"/> contains the <paramref name="expected"/> errors.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertErrors(params ApiError[] expected)
        {
            AssertBadRequestErrors(expected, true);
            return this;
        }

        /// <summary>
        /// Asserts the expected vs actual errors.
        /// </summary>
        private void AssertBadRequestErrors(IEnumerable<ApiError> expected, bool includeField)
        {
            var val = GetValue<Dictionary<string, string[]>>() ?? new Dictionary<string, string[]>();
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
        public TResult? GetValue<TResult>()
        {
            _implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            return _jsonSerializer.Deserialize<TResult>(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        }

        /// <summary>
        /// Gets the response content as the deserialized JSON <typeparamref name="TCollResult"/> value.
        /// </summary>
        /// <typeparam name="TCollResult">The <see cref="ICollectionResult{TColl, TItem}"/> response <see cref="Type"/>.</typeparam>
        /// <typeparam name="TColl">The collection <see cref="Type"/>.</typeparam>
        /// <typeparam name="TItem">The item <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        public TCollResult? GetValue<TCollResult, TColl, TItem>()
            where TCollResult : ICollectionResult<TColl, TItem>, new()
            where TColl : ICollection<TItem>
        {
            _implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            var content = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            try
            {
                var result = new TCollResult { Collection = _jsonSerializer.Deserialize<TColl>(content)! };

                if (Response.TryGetPagingResult(out var paging))
                    result.Paging = paging;

                return result;
            }
            catch (Exception ex)
            {
                _implementor.AssertFail($"Unable to deserialize the JSON content to Type {typeof(TColl).FullName}: {ex.Message}");
                return default!;
            }
        }

        /// <summary>
        /// Gets the response content as a <see cref="string"/>.
        /// </summary>
        /// <returns>The result content <see cref="string"/>.</returns>
        public string? GetContent()
        {
            if (Response.Content == null)
                return null;

            return Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}