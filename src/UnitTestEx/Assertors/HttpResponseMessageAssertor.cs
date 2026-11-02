// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="HttpResponseMessage"/> test assert helper.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBaseCore"/>.</param>
    /// <param name="logs">The log messages captured during execution.</param>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    public class HttpResponseMessageAssertor(TesterBaseCore owner, IEnumerable<string?>? logs, HttpResponseMessage response) : HttpResponseMessageAssertorBase<HttpResponseMessageAssertor>(owner, logs, response)
    {
        /// <summary>
        /// Executes a deferred result in a temporary buffer and restores the original response stream on every exit path.
        /// </summary>
        /// <param name="owner">The owning tester.</param>
        /// <param name="logs">The invocation logs.</param>
        /// <param name="request">The optional request whose context is used for execution.</param>
        /// <param name="execute">The deferred result executor.</param>
        /// <returns>The assertor for the rendered response.</returns>
        internal static HttpResponseMessageAssertor ExecuteResult(TesterBase owner, IEnumerable<string?>? logs, HttpRequest? request, Func<HttpContext, Task> execute)
        {
            var stopwatch = Stopwatch.StartNew();
            var context = request?.HttpContext ?? new DefaultHttpContext { RequestServices = owner.Services };
            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            try
            {
                context.Response.Body = buffer;
                execute(context).GetAwaiter().GetResult();
                return Create(owner, context.Response, buffer.ToArray(), logs, stopwatch);
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }

        /// <summary>
        /// Copies an already-written response while preserving ownership and position of its buffered body.
        /// </summary>
        /// <param name="owner">The owning tester.</param>
        /// <param name="response">The already-written response.</param>
        /// <param name="logs">The invocation logs.</param>
        /// <returns>An assertor owning an independent copy of the response bytes and headers.</returns>
        internal static HttpResponseMessageAssertor Snapshot(TesterBaseCore owner, HttpResponse response, IEnumerable<string?>? logs)
        {
            var stopwatch = Stopwatch.StartNew();
            var body = response.Body;
            if (!body.CanRead || !body.CanSeek)
                throw new NotSupportedException("Cannot snapshot the HTTP response body because it is not readable and seekable. Assign a readable, seekable buffer (such as MemoryStream) to HttpContext.Response.Body before invoking the function, and keep it open until after the snapshot.");

            var position = body.Position;
            using var buffer = new MemoryStream();
            try
            {
                body.Position = 0;
                body.CopyTo(buffer);
            }
            finally
            {
                body.Position = position;
            }

            return Create(owner, response, buffer.ToArray(), logs, stopwatch);
        }

        /// <summary>
        /// Builds and logs an HTTP response message from response metadata and captured bytes.
        /// </summary>
        /// <param name="owner">The owning tester.</param>
        /// <param name="response">The source response metadata.</param>
        /// <param name="body">The captured response bytes.</param>
        /// <param name="logs">The invocation logs.</param>
        /// <param name="stopwatch">The capture timer.</param>
        /// <returns>The assertor for the copied response.</returns>
        private static HttpResponseMessageAssertor Create(TesterBaseCore owner, HttpResponse response, byte[] body, IEnumerable<string?>? logs, Stopwatch stopwatch)
        {
            var message = new HttpResponseMessage((System.Net.HttpStatusCode)response.StatusCode)
            {
                Content = new ByteArrayContent(body)
            };
            foreach (var header in response.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]))
                    message.Content.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]);
            }

            stopwatch.Stop();
            owner.LogHttpResponseMessage(message, stopwatch);
            return new HttpResponseMessageAssertor(owner, logs, message);
        }

        /// <summary>
        /// Asserts the the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> matches the <paramref name="expectedUri"/>.
        /// </summary>
        /// <param name="expectedUri">The expected <see cref="Uri"/>.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertLocationHeader(Uri expectedUri)
        {
            Implementor.AssertAreEqual(expectedUri, Response.Headers?.Location, $"Expected and Actual HTTP Response Header '{HeaderNames.Location}' values are not equal.");
            return this;
        }

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
        /// Asserts the the <see cref="HttpResponseMessageAssertorBase.Response"/> <see cref="HttpResponseMessage.Headers"/> <see cref="HeaderNames.Location"/> contains the <paramref name="expected"/> string.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertLocationHeaderContains(string expected)
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
        /// <returns>The <see cref="HttpResponseMessageAssertor"/> to support fluent-style method-chaining.</returns>
        public HttpResponseMessageAssertor AssertLocationHeaderContains<TValue>(Func<TValue?, string> expected)
            => AssertLocationHeaderContains(expected?.Invoke(GetValue<TValue>())!);

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

            if (!string.IsNullOrEmpty(Response.Content.Headers?.ContentType?.MediaType) && TesterBaseCore.JsonMediaTypeNames.Contains(Response.Content.Headers.ContentType.MediaType!))
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