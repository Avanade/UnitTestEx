// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Provdes the base <see cref="HttpResponseMessage"/> test assert helper capabilities.
    /// </summary>
    public abstract class HttpResponseMessageAssertorBase<TSelf> where TSelf : HttpResponseMessageAssertorBase<TSelf>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertorBase{TSelf}"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertorBase(HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer)
        {
            Response = response;
            Implementor = implementor;
            JsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Gets the <see cref="HttpResponseMessage"/>.
        /// </summary>
        public HttpResponseMessage Response { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer { get; }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessage.StatusCode"/> has a successful value between 200 and 299.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertSuccessStatusCode()
        {
            var sc = (int)Response.StatusCode;
            if (sc < 200 || sc > 299)
                Implementor.AssertFail($"Result Status Code '{sc}' must be between 200 and 299 to be considered successful.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> has the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf Assert(HttpStatusCode statusCode)
        {
            Implementor.AssertAreEqual(statusCode, Response.StatusCode);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> has the specified <paramref name="statusCode"/> and <paramref name="content"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="content">The expected content.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf Assert(HttpStatusCode statusCode, string? content)
        {
            Assert(statusCode);
            Implementor.AssertAreEqual(content, Response.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> content type is <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentTypeJson() => AssertContentType(MediaTypeNames.Application.Json);

        /// <summary>
        /// Asserts that the <see cref="Response"/> content type is <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentTypePlainText() => AssertContentType(MediaTypeNames.Text.Plain);

        /// <summary>
        /// Asserts that the <see cref="Response"/> content type matches the <paramref name="expectedContentType"/>.
        /// </summary>
        /// <param name="expectedContentType">The expected content type.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentType(string expectedContentType)
        {
            Implementor.AssertAreEqual(expectedContentType, Response?.Content?.Headers?.ContentType?.MediaType);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNotFound() => Assert(HttpStatusCode.NotFound);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNoContent() => Assert(HttpStatusCode.NoContent);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.OK"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertOK() => Assert(HttpStatusCode.OK);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.NotModified"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNotModified() => Assert(HttpStatusCode.NotModified);

        /// <summary>
        /// Asserts that the <see cref="Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedETag"/>.
        /// </summary>
        /// <param name="expectedETag">The expected ETag value.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertETagHeader(string expectedETag)
        {
            Implementor.AssertAreEqual(expectedETag, Response.Headers?.ETag?.Tag, $"Expected and Actual HTTP Response Header '{HeaderNames.ETag}' values are not equal.");
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertCreated() => Assert(HttpStatusCode.Created);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is <see cref="HttpStatusCode.Accepted"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertAccepted() => Assert(HttpStatusCode.Accepted);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.PreconditionFailed"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertPreconditionFailed() => Assert(HttpStatusCode.PreconditionFailed);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Conflict"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertConflict() => Assert(HttpStatusCode.Conflict);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Unauthorized"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertUnauthorized() => Assert(HttpStatusCode.Unauthorized);

        /// <summary>
        /// Asserts that the <see cref="Response"/> is a <see cref="HttpStatusCode.Forbidden"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertForbidden() => Assert(HttpStatusCode.Forbidden);

        /// <summary>
        /// Asserts that the <see cref="Response"/> contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public TSelf AssertErrors(params string[] messages)
        {
            var expected = new List<ApiError>();
            foreach (var m in messages)
            {
                expected.Add(new ApiError(null, m));
            }

            AssertBadRequestErrors(expected, false);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="Response"/> contains the <paramref name="expected"/> errors.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertErrors(params ApiError[] expected)
        {
            AssertBadRequestErrors(expected, true);
            return (TSelf)this;
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
                Implementor.AssertFail(message);
        }

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <typeparam name="TValue">The resulting value <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        protected TValue? GetValue<TValue>()
        {
            Implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            return Expectations.HttpResponseExpectations.GetValueFromHttpResponseMessage<TValue>(Response, JsonSerializer);
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

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> JSON content matches the JSON from the embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertJsonFromResource(string resourceName, params string[] pathsToIgnore) => AssertJson(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase{TSelf}.Response"/> JSON content matches the specified <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertJson(string json, params string[] pathsToIgnore)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            if (Response.Content == null)
            {
                Implementor.AssertAreEqual(json, null, "Expected and Actual (no content) JSON values are not equal");
                return (TSelf)this;
            }

            if (Response.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                var exp = new Utf8JsonReader(new BinaryData(json));
                if (!JsonElement.TryParseValue(ref exp, out JsonElement? eje))
                    throw new ArgumentException("Expected JSON is not considered valid.", nameof(json));

                var act = new Utf8JsonReader(new BinaryData(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                if (!JsonElement.TryParseValue(ref act, out JsonElement? aje))
                    Implementor.AssertFail("Actual value is not considered valid JSON.");

                var jecr = new JsonElementComparer(5).Compare(eje!.Value, aje!.Value, pathsToIgnore);
                if (jecr != null)
                    Implementor.AssertFail($"Expected and Actual JSON values are not equal:{Environment.NewLine}{jecr}");
            }
            else
                Implementor.AssertAreEqual(json, Response.Content?.ReadAsStringAsync().GetAwaiter().GetResult(), "Expected and Actual JSON values are not equal.");

            return (TSelf)this;
        }
    }
}