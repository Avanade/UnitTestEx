// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Provdes the base <see cref="HttpResponseMessage"/> test assert helper capabilities.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    public abstract class HttpResponseMessageAssertorBase<TSelf>(TesterBase owner, HttpResponseMessage response) : HttpResponseMessageAssertorBase(owner, response) where TSelf : HttpResponseMessageAssertorBase<TSelf>
    {
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
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> has the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf Assert(HttpStatusCode statusCode)
        {
            Implementor.AssertAreEqual(statusCode, Response.StatusCode);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> has the specified <paramref name="statusCode"/> and <paramref name="content"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="content">The expected content.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf Assert(HttpStatusCode statusCode, string? content)
        {
            Assert(statusCode);
            AssertContent(content);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> content type is <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentTypeJson() => AssertContentType(MediaTypeNames.Application.Json);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> content type is <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentTypePlainText() => AssertContentType(MediaTypeNames.Text.Plain);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> content type matches the <paramref name="expectedContentType"/>.
        /// </summary>
        /// <param name="expectedContentType">The expected content type.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContentType(string expectedContentType)
        {
            Implementor.AssertAreEqual(expectedContentType, Response?.Content?.Headers?.ContentType?.MediaType);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> has the specified <paramref name="content"/>.
        /// </summary>
        /// <param name="content">The expected content.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertContent(string? content)
        {
            Implementor.AssertAreEqual(content, Response.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNotFound() => Assert(HttpStatusCode.NotFound);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNoContent() => Assert(HttpStatusCode.NoContent);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.OK"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertOK() => Assert(HttpStatusCode.OK);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.NotModified"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNotModified() => Assert(HttpStatusCode.NotModified);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.ETag"/> matches the <paramref name="expectedETag"/>.
        /// </summary>
        /// <param name="expectedETag">The expected ETag value.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>An <paramref name="expectedETag"/> of <c>null</c> will confirm existence only, not the actual value.</remarks>
        public TSelf AssertETagHeader(string? expectedETag = null)
        {
            if (expectedETag is null)
            {
                if (Response.Headers?.ETag is null || Response.Headers?.ETag?.Tag is null)
                    Implementor.AssertFail($"Expected an '{HeaderNames.ETag}' HTTP Response Header with a value; none was found.");
            }
            else
                Implementor.AssertAreEqual(expectedETag, Response.Headers?.ETag?.Tag, $"Expected and Actual HTTP Response Header '{HeaderNames.ETag}' values are not equal.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.ETag"/> matches the <paramref name="expectedETag"/>.
        /// </summary>
        /// <param name="expectedETag">The expected <see cref="System.Net.Http.Headers.EntityTagHeaderValue"/> value.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertETagHeader(System.Net.Http.Headers.EntityTagHeaderValue expectedETag)
        {
            Implementor.AssertAreEqual(expectedETag, Response.Headers?.ETag, $"Expected and Actual HTTP Response Header '{HeaderNames.ETag}' values are not equal.");
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertCreated() => Assert(HttpStatusCode.Created);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is <see cref="HttpStatusCode.Accepted"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertAccepted() => Assert(HttpStatusCode.Accepted);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is a <see cref="HttpStatusCode.PreconditionFailed"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertPreconditionFailed() => Assert(HttpStatusCode.PreconditionFailed);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is a <see cref="HttpStatusCode.Conflict"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertConflict() => Assert(HttpStatusCode.Conflict);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is a <see cref="HttpStatusCode.Unauthorized"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertUnauthorized() => Assert(HttpStatusCode.Unauthorized);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> is a <see cref="HttpStatusCode.Forbidden"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertForbidden() => Assert(HttpStatusCode.Forbidden);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public TSelf AssertErrors(params string[] messages)
        {
            var expected = new List<ApiError>();
            foreach (var message in messages)
                expected.Add(new ApiError(null, message));

            return AssertErrors(expected.ToArray());
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> contains the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected errors.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertErrors(params ApiError[] errors)
        {
            IDictionary<string, string[]>? val;

            try
            {
                val = GetValue<IDictionary<string, string[]>>(null);
            }
            catch (Exception)
            {
                try
                {
                    val = GetValue<HttpValidationProblemDetails>(null)?.Errors;
                }
                catch (Exception ex)
                {
                    Implementor.AssertFail($"Unable to deserialize the errors from either 'IDictionary<string, string[]>' or 'HttpValidationProblemDetails': {ex.Message}");
                    return (TSelf)this;
                }
            }

            var actual = Assertor.ConvertToApiErrors(val);
            if (!Assertor.TryAreErrorsMatched(errors, actual, out var errorMessage))
                Implementor.AssertFail(errorMessage);

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> JSON content matches the JSON from the embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertJsonFromResource(string resourceName, params string[] pathsToIgnore) => AssertJson(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertJsonFromResource<TAssembly>(string resourceName, params string[] pathsToIgnore) => AssertJson(Resource.GetJson(resourceName, typeof(TAssembly).Assembly), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="HttpResponseMessageAssertorBase.Response"/> JSON content matches the specified <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
#if NET7_0_OR_GREATER
        public TSelf AssertJson([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore)
#else
        public TSelf AssertJson(string json, params string[] pathsToIgnore)
#endif
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            if (Response.Content == null)
            {
                Implementor.AssertAreEqual(json, null, "Expected and Actual (no content) JSON values are not equal");
                return (TSelf)this;
            }

            var jc = Owner.CreateJsonComparer().Compare(json, Response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), pathsToIgnore);
            if (jc.HasDifferences)
                Implementor.AssertFail($"Expected and Actual JSON values are not equal:{Environment.NewLine}{jc}");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that a response header exists with the specified <paramref name="name"/> and contains the specified <paramref name="values"/>.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="values">The expected header value(s).</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNamedHeader(string name, params string[] values)
        {
            if (Response.Headers.TryGetValues(name ?? throw new ArgumentNullException(nameof(name)), out var hvals))
                Implementor.AssertAreEqual(string.Join(", ", values), string.Join(", ", hvals), $"Expected and Actual '{name}' header values are not equal.");
            else
                Implementor.AssertFail($"The '{name}' header was not found.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that a response header does not exist with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertorBase{TSelf}"/> instance to support fluent-style method-chaining.</returns>
        public TSelf AssertNoNamedHeader(string name)
        {
            if (Response.Headers.TryGetValues(name ?? throw new ArgumentNullException(nameof(name)), out var _))
                Implementor.AssertFail($"The '{name}' header was found when it was expected to be absent.");

            return (TSelf)this;
        }
    }
}