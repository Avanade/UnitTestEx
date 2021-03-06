// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Http;
using CoreEx.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using Ceh = CoreEx.Http;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the base HTTP testing capabilities.
    /// </summary>
    public abstract class HttpTesterBase
    {
        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTesterBase(TesterBase owner, TestServer testServer)
        {
            Owner = owner;
            TestServer = testServer;
        }

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; }

        /// <summary>
        /// Gets the <see cref="TestServer"/>.
        /// </summary>
        protected internal TestServer TestServer { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor => Owner.Implementor;

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer => Owner.JsonSerializer;

        /// <summary>
        /// Sends with no content.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestOptions">The <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">The <see cref="Ceh.IHttpArg"/> array.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
        {
            var res = await new TypedHttpClient(this).SendAsync(httpMethod, requestUri, requestOptions, args).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(res, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Sends with <paramref name="content"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="content">The content.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="requestOptions">The <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">The <see cref="Ceh.IHttpArg"/> array.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, string? content, string? contentType, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
        {
            if (content != null && httpMethod == HttpMethod.Get)
                Implementor.CreateLogger("ApiTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var res = await new TypedHttpClient(this).SendAsync(httpMethod, requestUri, content, contentType, requestOptions, args).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(res, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Sends with JSON <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The value to be JSON serialized.</param>
        /// <param name="requestOptions">The <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">The <see cref="Ceh.IHttpArg"/> array.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected async Task<HttpResponseMessageAssertor> SendAsync(HttpMethod httpMethod, string? requestUri, object? value, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
        {
            if (value != null && httpMethod == HttpMethod.Get)
                Implementor.CreateLogger("ApiTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var res = await new TypedHttpClient(this).SendAsync(httpMethod, requestUri, value, requestOptions, args).ConfigureAwait(false);
            return new HttpResponseMessageAssertor(res, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Sends using the <paramref name="func"/>.
        /// </summary>
        /// <param name="func">The function to execute the HTTP request.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected Task<HttpResponseMessageAssertor> RunAsync<TAgent>(Func<TAgent, Task<HttpResult>> func) where TAgent : TypedHttpClientBase
            => RunWrapperAsync<TAgent>(async a => new HttpResponseMessageAssertor((await (func ?? throw new ArgumentNullException(nameof(func))).Invoke(a).ConfigureAwait(false)).Response, Implementor, JsonSerializer));

        /// <summary>
        /// Sends using the <paramref name="func"/>.
        /// </summary>
        /// <param name="func">The function to execute the HTTP request.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected Task<HttpResponseMessageAssertor> RunAsync<TAgent>(Func<TAgent, Task<HttpResponseMessage>> func) where TAgent : TypedHttpClientBase
            => RunWrapperAsync<TAgent>(async a => new HttpResponseMessageAssertor((await (func ?? throw new ArgumentNullException(nameof(func))).Invoke(a).ConfigureAwait(false)), Implementor, JsonSerializer));

        /// <summary>
        /// Sends using the <paramref name="func"/>.
        /// </summary>
        /// <param name="func">The function to execute the HTTP request.</param>
        /// <returns>The <see cref="HttpResponseMessageAssertor"/>.</returns>
        protected Task<HttpResponseMessageAssertor> RunAsync<TAgent, TResponse>(Func<TAgent, Task<HttpResult<TResponse>>> func) where TAgent : TypedHttpClientBase
            => RunWrapperAsync<TAgent>(async a => new HttpResponseMessageAssertor((await (func ?? throw new ArgumentNullException(nameof(func))).Invoke(a).ConfigureAwait(false)).Response, Implementor, JsonSerializer));

        /// <summary>
        /// Wraps and runs the function.
        /// </summary>
        private async Task<HttpResponseMessageAssertor> RunWrapperAsync<TAgent>(Func<TAgent, Task<HttpResponseMessageAssertor>> func) where TAgent : TypedHttpClientBase
        {
            var sc = new ServiceCollection();
            sc.AddExecutionContext();
            sc.AddSingleton(JsonSerializer);
            sc.AddLogging(c => { c.ClearProviders(); c.AddProvider(Implementor.CreateLoggerProvider()); });
            sc.AddSingleton(new HttpClient(new LoggingDelegatingHandler(this, TestServer.CreateHandler())) { BaseAddress = TestServer.BaseAddress });
            sc.AddSingleton(Owner.Configuration);
            sc.AddDefaultSettings();
            sc.AddScoped<TAgent>();

            using var scope = sc.BuildServiceProvider().CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<TAgent>();

            var resp = await func(agent).ConfigureAwait(false);

            scope.Dispose();
            return resp;
        }

        /// <summary>
        /// Creates an <see cref="HttpClient"/> for the <see cref="TestServer"/> that logs the request and response to the test output.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        protected HttpClient CreateHttpClient() => new(new LoggingDelegatingHandler(this, TestServer.CreateHandler())) { BaseAddress = TestServer.BaseAddress };

        private class LoggingDelegatingHandler : DelegatingHandler
        {
            private readonly HttpTesterBase _httpTester;

            /// <summary>
            /// Initialize a new instance of the class.
            /// </summary>
            public LoggingDelegatingHandler(HttpTesterBase httpTester, HttpMessageHandler innerHandler) : base(innerHandler) => _httpTester = httpTester;

            /// <inheritdoc/>
            protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_httpTester.Owner.SetUp.OnBeforeHttpRequestMessageSendAsync != null)
                    await _httpTester.Owner.SetUp.OnBeforeHttpRequestMessageSendAsync(request, _httpTester.Owner.Username, cancellationToken);

                _httpTester.LogRequest(request);
                var sw = Stopwatch.StartNew();
                var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                await Task.Delay(0, cancellationToken).ConfigureAwait(false);
                _httpTester.LogResponse(res, sw);
                return res;
            }
        }

        /// <summary>
        /// Provides the requisite <see cref="HttpClient"/> sending capabilities.
        /// </summary>
        private class TypedHttpClient : Ceh.TypedHttpClientBase
        {
            /// <summary>
            /// Initialize a new instance of the class.
            /// </summary>
            public TypedHttpClient(HttpTesterBase httpTester) : base(httpTester.CreateHttpClient(), httpTester.JsonSerializer) { }

            /// <summary>
            /// Sends with no content.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
                => await SendAsync(await CreateRequestAsync(method, requestUri ?? "", requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false);

            /// <summary>
            /// Sends with content.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, string? content, string? contentType, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
                => await SendAsync(await CreateContentRequestAsync(method, requestUri ?? "", new StringContent(content ?? string.Empty, Encoding.UTF8, contentType ?? MediaTypeNames.Text.Plain), requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false);

            /// <summary>
            /// Sends with JSON value.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, object? value, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
                => await SendAsync(await CreateJsonRequestAsync(method, requestUri ?? "", value, requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false);

            /// <inheritdoc/>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Client.SendAsync(request, cancellationToken);
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

                Implementor.WriteLine("");
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

                Implementor.WriteLine($"Content: [{req.Content?.Headers?.ContentType?.MediaType ?? "None"}]");
                Implementor.WriteLine(jo == null ? content : JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }

            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
        }

        /// <summary>
        /// Log the response to the output.
        /// </summary>
        private void LogResponse(HttpResponseMessage res, Stopwatch sw)
        {
            if (res.RequestMessage == null)
                return;

            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"HttpStatusCode: {res.StatusCode} ({(int)res.StatusCode})");
            Implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

            var hdrs = res.Headers?.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Implementor.WriteLine($"Headers: {(hdrs == null || !hdrs.Any() ? "none" : "")}");
            if (hdrs != null && hdrs.Any())
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

            // Assert any expectations.
            AssertExpectations(res);
        }

        /// <summary>
        /// Perform the assertion of any expectations.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>/</param>
        protected abstract void AssertExpectations(HttpResponseMessage res);
    }
}