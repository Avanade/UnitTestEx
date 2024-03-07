// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Json;
using UnitTestEx.Mocking;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the base HTTP testing capabilities.
    /// </summary>
    public abstract class HttpTesterBase
    {
        /// <summary>
        /// Gets the '<c>unit-test-ex-request-id</c>' constant.
        /// </summary>
        public const string RequestIdName = "unit-test-ex-request-id";

        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        public HttpTesterBase(TesterBase owner, TestServer testServer)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            TestServer = testServer ?? throw new ArgumentNullException(nameof(testServer));
            UserName = Owner.UserName;
        }

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; }

        /// <summary>
        /// Gets the underlying <see cref="TestServer"/>.
        /// </summary>
        public TestServer TestServer { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor => Owner.Implementor;

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer => Owner.JsonSerializer;

        /// <summary>
        /// Gets or sets the test user name (defaults to <see cref="TesterBase.UserName"/>).
        /// </summary>
        public string? UserName { get; protected set; }

        /// <summary>
        /// Gets the unqiue request identifier.
        /// </summary>
        /// <remarks>This value is related to the <see cref="RequestIdName"/>.</remarks>
        public string RequestId { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the last set of logs.
        /// </summary>
        protected IEnumerable<string?>? LastLogs { get; set; }

        /// <summary>
        /// Sends with no content.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, Action<HttpRequestMessage>? requestModifier)
        {
            using var client = CreateHttpClient();
            var res = await new TypedHttpClient(client, JsonSerializer).SendAsync(httpMethod, requestUri, requestModifier).ConfigureAwait(false);
            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            await AssertExpectationsAsync(res).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(Owner, res);
        }

        /// <summary>
        /// Sends with <paramref name="content"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="content">The content.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, string? content, string? contentType, Action<HttpRequestMessage>? requestModifier)
        {
            if (content != null && httpMethod == HttpMethod.Get)
                Owner.LoggerProvider.CreateLogger("ApiTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            using var client = CreateHttpClient();
            var res = await new TypedHttpClient(client, JsonSerializer).SendAsync(httpMethod, requestUri, content, contentType, requestModifier).ConfigureAwait(false);
            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            await AssertExpectationsAsync(res).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(Owner, res);
        }

        /// <summary>
        /// Sends with JSON <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The value to be JSON serialized.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, object? value, Action<HttpRequestMessage>? requestModifier)
        {
            if (value != null && httpMethod == HttpMethod.Get)
                Owner.LoggerProvider.CreateLogger("ApiTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            using var client = CreateHttpClient();
            var res = await new TypedHttpClient(client, JsonSerializer).SendAsync(httpMethod, requestUri, value, requestModifier).ConfigureAwait(false);
            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            await AssertExpectationsAsync(res).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(Owner, res);
        }

        /// <summary>
        /// Creates an <see cref="HttpClient"/> for the <see cref="TestServer"/> that logs the request and response to the test output.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        public HttpClient CreateHttpClient() => new(new HttpDelegatingHandler(this, TestServer.CreateHandler())) { BaseAddress = TestServer.BaseAddress };

        /// <summary>
        /// Orchestrates the HTTP request send including logging and <see cref="TestSetUp.OnBeforeHttpRequestMessageSendAsync"/>.
        /// </summary>
        /// <param name="httpTester">The <see cref="HttpTesterBase"/>.</param>
        /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
        public class HttpDelegatingHandler(HttpTesterBase httpTester, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
        {
            private readonly HttpTesterBase _httpTester = httpTester;

            /// <inheritdoc/>
            protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_httpTester.Owner.SetUp.OnBeforeHttpRequestMessageSendAsync != null)
                    await _httpTester.Owner.SetUp.OnBeforeHttpRequestMessageSendAsync(request, _httpTester.UserName, cancellationToken);

                request.Headers.Add(RequestIdName, _httpTester.RequestId);

                _httpTester.LogRequest(request);
                var sw = Stopwatch.StartNew();
                var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                await Task.Delay(0, cancellationToken).ConfigureAwait(false);
                _httpTester.LastLogs = _httpTester.Owner.SharedState.GetLoggerMessages(_httpTester.RequestId);
                _httpTester.LogResponse(res, sw, _httpTester.LastLogs);
                return res;
            }
        }

        /// <summary>
        /// Provides the requisite <see cref="HttpClient"/> sending capabilities.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        public class TypedHttpClient(HttpClient client, IJsonSerializer jsonSerializer)
        {
            private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));
            private readonly IJsonSerializer _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));

            /// <summary>
            /// Sends with no content.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, Action<HttpRequestMessage>? requestModifier)
                => await SendAsync(CreateRequest(method, requestUri ?? "", null, requestModifier), default).ConfigureAwait(false);

            /// <summary>
            /// Sends with content.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, string? content, string? contentType, Action<HttpRequestMessage>? requestModifier)
                => await SendAsync(CreateRequest(method, requestUri ?? "", new StringContent(content ?? string.Empty, Encoding.UTF8, contentType ?? MediaTypeNames.Text.Plain), requestModifier), default).ConfigureAwait(false);

            /// <summary>
            /// Sends with JSON value.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, object? value, Action<HttpRequestMessage>? requestModifier)
                => await SendAsync(CreateRequest(method, requestUri ?? "", new StringContent(_jsonSerializer.Serialize(value), Encoding.UTF8, MediaTypeNames.Application.Json), requestModifier), default).ConfigureAwait(false);

            /// <inheritdoc/>
            private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _client.SendAsync(request, cancellationToken);

            /// <summary>
            /// Create the request.
            /// </summary>
            private static HttpRequestMessage CreateRequest(HttpMethod method, string requestUri, HttpContent? content, Action<HttpRequestMessage>? requestModifier)
            {
                var uri = new Uri(requestUri, UriKind.RelativeOrAbsolute);
                var ub = new UriBuilder(uri.IsAbsoluteUri ? uri : new Uri(MockHttpClient.DefaultBaseAddress, requestUri));

                var request = new HttpRequestMessage(method, ub.Uri);
                if (content != null)
                    request.Content = content;

                requestModifier?.Invoke(request);

                return request;
            }
        }

        /// <summary>
        /// Log the request to the output.
        /// </summary>
        private void LogRequest(HttpRequestMessage req)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("API TESTER...");
            Implementor.WriteLine("");
            Implementor.WriteLine("REQUEST >");
            Implementor.WriteLine($"Request: {req.Method} {req.RequestUri}");
            Implementor.WriteLine($"Headers: {(req.Headers == null || !req.Headers.Any() ? "none" : "")}");
            if (req.Headers != null && req.Headers.Any())
            {
                foreach (var hdr in req.Headers)
                {
                    var sb = new StringBuilder();
                    foreach (var v in hdr.Value)
                    {
                        if (sb.Length > 0)
                            sb.Append(", ");

                        sb.Append(v);
                    }

                    Implementor.WriteLine($"  {hdr.Key}: {sb}");
                }
            }

            object? jo = null;
            if (req.Content != null)
            {
                string? content = null;

                // Parse out the content.
                try
                {
                    content = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(content))
                        jo = JsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }

                if (req.Headers != null && req.Headers.Any())
                    Implementor.WriteLine("");

                Implementor.WriteLine($"Content: [{req.Content?.Headers?.ContentType?.MediaType ?? "None"}]");
                Implementor.WriteLine(jo == null ? content : JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }
        }

        /// <summary>
        /// Log the response to the output.
        /// </summary>
        private void LogResponse(HttpResponseMessage res, Stopwatch sw, IEnumerable<string?>? logs)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            if (logs is not null && logs.Any())
            {
                foreach (var msg in logs)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            if (res.RequestMessage == null)
                return;

            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"HttpStatusCode: {res.StatusCode} ({(int)res.StatusCode})");
            Implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

            var hdrs = res.Headers?.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Implementor.WriteLine($"Headers: {(hdrs == null || hdrs.Length == 0 ? "none" : "")}");
            if (hdrs != null && hdrs.Length > 0)
            {
                foreach (var hdr in hdrs)
                {
                    Implementor.WriteLine($"  {hdr}");
                }
            }

            object? jo = null;
            var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(content) && res.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                try
                {
                    jo = JsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }
            }

            var txt = $"Content: [{res.Content?.Headers?.ContentType?.MediaType ?? "none"}]";
            if (jo != null)
            {
                Implementor.WriteLine(txt);
                Implementor.WriteLine(JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }
            else
                Implementor.WriteLine($"{txt} {(string.IsNullOrEmpty(content) ? "none" : content)}");

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }

        /// <summary>
        /// Perform the assertion of any expectations.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>/</param>
        protected abstract Task AssertExpectationsAsync(HttpResponseMessage res);
    }
}