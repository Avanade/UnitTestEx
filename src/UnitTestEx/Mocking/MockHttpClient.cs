// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="System.Net.Http.HttpClient"/> (more specifically <see cref="HttpMessageHandler"/>) mocking.
    /// </summary>
    public sealed class MockHttpClient : IDisposable
    {
        /// <summary>
        /// Gets the default <see cref="HttpClient.BaseAddress"/> being '<c>https://unittest</c>'.
        /// </summary>
        public static Uri DefaultBaseAddress { get; } = new Uri("https://unittest");

        private readonly Uri? _baseAddress;
        private readonly List<MockHttpClientRequest> _requests = [];
        private readonly object _lock = new();
        private HttpClient? _httpClient;
        private bool _noMocking;
        private bool _useHttpMessageHandlers;
        private Type[] _excludeTypes = [];
        private bool _useHttpClientConfigurations;
        private bool _traceRequestComparisons;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClient"/> class.
        /// </summary>
        /// <param name="factory">The <see cref="MockHttpClientFactory"/>.</param>
        /// <param name="name">The logical name of the client.</param>
        /// <param name="baseAddress">The base Uniform Resource Identifier (URI) of the Internet resource used when sending requests; defaults to <see cref="DefaultBaseAddress"/> where not specified.</param>
        internal MockHttpClient(MockHttpClientFactory factory, string name, Uri? baseAddress)
        {
            Factory = factory;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _baseAddress = baseAddress;
            IsBaseAddressSpecified = baseAddress is not null;
            Factory.HttpClientFactory.Setup(x => x.CreateClient(It.Is<string>(x => x == name))).Returns(GetHttpClient);
        }

        /// <summary>
        /// Gets the <see cref="MockHttpClientFactory"/>.
        /// </summary>
        internal MockHttpClientFactory Factory { get; }

        /// <summary>
        /// Gets the logical name of the client.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the <see cref="Mock"/> <see cref="HttpMessageHandler"/>.
        /// </summary>
        /// <remarks>This is not used when <see cref="WithoutMocking(Type[])"/> is used.</remarks>
        internal Mock<HttpMessageHandler> MessageHandler { get; } = new Mock<HttpMessageHandler>();

        /// <summary>
        /// Indicates whether the <see cref="HttpClient.BaseAddress"/> is explicitly specified (overridden) for the test; otherwise, will use as configured or default to <see cref="DefaultBaseAddress"/>.
        /// </summary>
        internal bool IsBaseAddressSpecified { get; set; }

        /// <summary>
        /// Gets the mocked <see cref="HttpClient"/>.
        /// </summary>
        /// <remarks>This will cache the <see cref="HttpClient"/> and reuse; the <see cref="Reset"/> can be used to clear and dispose.</remarks>
        public HttpClient GetHttpClient()
        {
            if (_httpClient is not null)
                return _httpClient;

            lock (_lock)
            {
                return _httpClient ??= CreateHttpClient();
            }
        }

        /// <summary>
        /// Create the <see cref="HttpClient"/>.
        /// </summary>
        private HttpClient CreateHttpClient()
        { 
            // Get the factory options where applicable.
            HttpClientFactoryOptions? options = null;
            if (Factory.Services is not null)
            {
                var om = Factory.Services.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
                options = om?.Get(Name);
            }
            else if (_noMocking)
            {
                // Where no mocking and no service provider then use the default HttpClient; that is all we can do.
                return new HttpClient(new HttpClientHandler(), true) { BaseAddress = _baseAddress ?? DefaultBaseAddress };
            }

            // Build the http handler.
            HttpClient httpClient;
            if (_useHttpMessageHandlers && options is not null)
            {
                var builder = new MockHttpMessageHandlerBuilder(Name, Factory.Services!, _noMocking ? new HttpClientHandler() : new MockHttpClientHandler(Factory, MessageHandler.Object), _excludeTypes);

                for (int i = 0; i < options.HttpMessageHandlerBuilderActions.Count; i++)
                {
                    options.HttpMessageHandlerBuilderActions[i](builder);
                }

                httpClient = new HttpClient(builder.Build(), true);
            }
            else
                httpClient = new HttpClient(new MockHttpClientHandler(Factory, MessageHandler.Object), true);

            // Configure the client where applicable.
            if (_useHttpClientConfigurations && options is not null)
            {
                for (int i = 0; i < options.HttpClientActions.Count; i++)
                {
                    options.HttpClientActions[i](httpClient);
                }
            }

            // Where a base address is specified, use it (override configuration).
            if (IsBaseAddressSpecified)
                httpClient.BaseAddress = _baseAddress;

            // Where no base address is specified or configured then default.
            if (!_noMocking)
                httpClient.BaseAddress ??= DefaultBaseAddress;

            return httpClient;
        }

        /// <summary>
        /// Specifies that the <see cref="HttpMessageHandler"/> and <see cref="GetHttpClient()"/> configurations are to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This is a combination of both <see cref="WithHttpClientConfigurations"/> and <see cref="WithHttpMessageHandlers(Type[])"/>.</remarks>
        public MockHttpClient WithConfigurations(params Type[] excludeTypes) => WithHttpClientConfigurations().WithHttpMessageHandlers(excludeTypes);

        /// <summary>
        /// Specifies that the <see cref="HttpMessageHandler"/> and <see cref="GetHttpClient()"/> configurations are <b>not</b> to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The is a combination of both <see cref="WithoutHttpClientConfigurations"/> and <see cref="WithoutHttpMessageHandlers"/>.</remarks>
        public MockHttpClient WithoutConfigurations() => WithoutHttpClientConfigurations().WithoutHttpMessageHandlers();

        /// <summary>
        /// Specifies that the <see cref="HttpMessageHandler"/> configurations for the <see cref="GetHttpClient()"/> are to be used.
        /// </summary>
        /// <param name="excludeTypes">The <see cref="HttpMessageHandler"/> types to be excluded.</param>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>By default the <see cref="HttpMessageHandler"/> configurations are not invoked.</remarks>
        public MockHttpClient WithHttpMessageHandlers(params Type[] excludeTypes)
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(WithHttpMessageHandlers)} is not supported where {nameof(WithoutMocking)} has been specified.");

            _useHttpMessageHandlers = true;
            _excludeTypes = excludeTypes;
            return this;
        }

        /// <summary>
        /// Specifies that the <see cref="HttpMessageHandler"/> configurations for the <see cref="GetHttpClient()"/> are <b>not</b> to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        public MockHttpClient WithoutHttpMessageHandlers()
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(WithoutHttpMessageHandlers)} is not supported where {nameof(WithoutMocking)} has been specified.");

            _useHttpMessageHandlers = false;
            _excludeTypes = [];
            return this;
        }

        /// <summary>
        /// Specifies that the configurations for the <see cref="GetHttpClient()"/> aer to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>By default the <see cref="GetHttpClient()"/> configurations are not invoked.</remarks>
        public MockHttpClient WithHttpClientConfigurations()
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(WithHttpClientConfigurations)} is not supported where {nameof(WithoutMocking)} has been specified.");

            _useHttpClientConfigurations = true;
            return this;
        }

        /// <summary>
        /// Specifies that the configurations for the <see cref="GetHttpClient()"/> are <b>not</b> to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        public MockHttpClient WithoutHttpClientConfigurations()
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(WithoutHttpClientConfigurations)} is not supported where {nameof(WithoutMocking)} has been specified.");

            _useHttpClientConfigurations = false;
            return this;
        }

        /// <summary>
        /// Specifies that the resulting <see cref="HttpClient"/> from the <see cref="GetHttpClient()"/> is to be instantiated with <b>no</b> mocking; i.e. will result in an actual/real HTTP request.
        /// </summary>
        /// <param name="excludeTypes">The <see cref="HttpMessageHandler"/> types to be excluded.</param>
        /// <remarks>Once set this is immutable.
        /// <para>As this results in no mocking, no specific usage tracking is then performed and as such the associated <see cref="Verify"/> will never assert anything but success.</para>
        /// <para><i>Note:</i> although this may imply that the native <see cref="HttpClient"/> and related <see cref="IHttpClientFactory"/> implementation is being leveraged, this is not the case. This is still using the <see cref="MockHttpClientFactory"/>
        /// to enable the <paramref name="excludeTypes"/> behavior leveraging the internal <see cref="HttpMessageHandlerBuilder"/> implementation. Best efforts have been made to achieve native-like functionality; however, some edge cases may not have been 
        /// accounted for.</para></remarks>
        public void WithoutMocking(params Type[] excludeTypes)
        {
            if (_requests.Count > 0)
                throw new InvalidOperationException($"{nameof(WithoutMocking)} is not supported where a {nameof(Request)} has already been specified.");

            _noMocking = true;
            _excludeTypes = excludeTypes;
            _useHttpMessageHandlers = true;
            _useHttpClientConfigurations = true;
        }

        /// <summary>
        /// Creates a new <see cref="MockHttpClientRequest"/> for the <see cref="GetHttpClient()"/>.
        /// </summary>
        /// <param name="method">The <see cref="HttpMethod"/>. Defaults to <see cref="HttpMethod.Get"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <returns>The <see cref="MockHttpClientRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public MockHttpClientRequest Request(HttpMethod? method = null, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri = null)
#else
        public MockHttpClientRequest Request(HttpMethod? method = null, string? requestUri = null)
#endif
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(Request)} is not supported where {nameof(WithoutMocking)} has been specified.");

            var r = new MockHttpClientRequest(this, method ?? HttpMethod.Get, requestUri);
            _requests.Add(r);
            if (_traceRequestComparisons)
                r.TraceRequestComparisons();

            return r;
        }

        /// <summary>
        /// Indicates whether the request content comparison differences should be trace logged to aid in debugging/troubleshooting.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>By default the request content comparison differences are not traced. Where tracing is requested all existing and new <see cref="Request(HttpMethod?, string?)"/> configurations
        /// will be traced.</remarks>
        public MockHttpClient TraceRequestComparisons()
        {
            _traceRequestComparisons = true;
            foreach (var r in _requests)
            {
                r.TraceRequestComparisons();
            }

            return this;
        }

        /// <summary>
        /// Verifies that all verifiable <see cref="Mock"/> expectations have been met; being all requests have been invoked.
        /// </summary>
        /// <remarks>This is a wrapper for '<c>MessageHandler.Verify()</c>' which can be invoked directly to leverage additional capabilities (overloads). Additionally, the <see cref="MockHttpClientRequest.Verify"/> is invoked for each 
        /// underlying <see cref="Request(HttpMethod, string)"/> to perform the corresponding <see cref="MockHttpClientRequest.Times(Times)"/> verification.<para>Note: no verify will occur where using sequences; this appears to be a
        /// limitation of MOQ.</para></remarks>
        public void Verify()
        {
            MessageHandler.Verify();

            foreach (var r in _requests)
            {
                r.Verify();
            }
        }

        /// <summary>
        /// Disposes and removes the cached <see cref="HttpClient"/>.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose() => Reset();

        /// <summary>
        /// Provides an internal/mocked <see cref="HttpMessageHandlerBuilder"/>
        /// </summary>
        private class MockHttpMessageHandlerBuilder(string? name, IServiceProvider services, HttpMessageHandler primaryHandler, Type[] excludeTypes) : HttpMessageHandlerBuilder
        {
            private readonly Type[] _excludeTypes = excludeTypes;

            /// <inheritdoc/>
            public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

            public override IServiceProvider Services { get; } = services;

            /// <inheritdoc/>
            public override string? Name { get; set; } = name;

            /// <inheritdoc/>
            public override HttpMessageHandler PrimaryHandler { get; set; } = primaryHandler;

            /// <inheritdoc/>
            public override HttpMessageHandler Build() => CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers.Where(x => !_excludeTypes.Contains(x.GetType())));
        }

        /// <summary>
        /// Adds mocked request(s) from the embedded resource formatted as either YAML or JSON.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <returns></returns>
        public MockHttpClient WithRequestsFromResource<TAssembly>(string resourceName) => WithRequestsFromResource(resourceName, typeof(TAssembly).Assembly);

        /// <summary>
        /// Adds mocked request(s) from the embedded resource formatted as either YAML or JSON.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        public MockHttpClient WithRequestsFromResource(string resourceName, Assembly? assembly = null)
        {
            ArgumentNullException.ThrowIfNull(resourceName, nameof(resourceName));

            bool isYaml = false;
            if (resourceName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || resourceName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                isYaml = true;
            else if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !resourceName.EndsWith(".jsn", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only YAML or JSON embedded resources are supported; the extension must be one of the following: .yaml, .yml, .json, .jsn", nameof(resourceName));

            using var sr = Resource.GetStream(resourceName, assembly ?? Assembly.GetCallingAssembly());
            var reqs = isYaml ? Resource.DeserializeYaml<List<MockConfigRequest>>(sr) : Resource.DeserializeJson<List<MockConfigRequest>>(sr);

            if (reqs is not null)
            {
                foreach (var req in reqs)
                {
                    req.Add(this);
                }
            }

            return this;
        }

        /// <summary>
        /// The mocked config contract for a Request.
        /// </summary>
        private class MockConfigRequest
        {
            public string? Method { get; set; }
            public string? Uri { get; set; }
            public string? Body { get; set; }
            public string? Media { get; set; }
            public string[]? Ignore { get; set; }
            public MockConfigResponse? Response { get; set; }
            public List<MockConfigResponse>? Sequence { get; set; }

            /// <summary>
            /// Adds the request and response to the client.
            /// </summary>
            public void Add(MockHttpClient client)
            {
                var req = client.Request(string.IsNullOrEmpty(Method) ? HttpMethod.Get : new HttpMethod(Method.ToUpperInvariant()), Uri).WithPathsToIgnore(Ignore ?? []);
                MockHttpClientResponse mres;

                if (Body is not null)
                {
                    if (Body == "^")
                        mres = req.WithAnyBody().Respond;
                    else
                    {
                        if (string.IsNullOrEmpty(Media))
                        {
                            try
                            {
                                _ = JsonDocument.Parse(Body);
                                Media = MediaTypeNames.Application.Json;
                            }
                            catch
                            {
                                Media = MediaTypeNames.Text.Plain;
                            }
                        }
                        
                        mres = req.WithBody(Body, Media).Respond;
                    }
                }
                else
                    mres = req.Respond;

                if (Response is not null && Sequence is not null)
                    throw new InvalidOperationException($"A mocked request can not contain both a {nameof(Response)} and a {nameof(Sequence)} as they are mutually exclusive.");

                // One-to-one response.
                if (Sequence is null)
                {
                    (Response ?? new()).Add(mres);
                    return;
                }

                // A sequence of responses.
                mres.WithSequence(seq =>
                {
                    foreach (var res in Sequence)
                    {
                        (res ?? new()).Add(seq.Respond());
                    }
                });

            }
        }

        /// <summary>
        /// The mocked config contract for a Response.
        /// </summary>
        private class MockConfigResponse
        {
            public HttpStatusCode? Status { get; set; }
            public string? Body { get; set; }
            public string? Media { get; set; }
            public Dictionary<string, string?[]>? Headers { get; set; }

            /// <summary>
            /// Adds the response.
            /// </summary>
            public void Add(MockHttpClientResponse res)
            {
                if (Headers is not null)
                {
                    foreach (var header in Headers)
                    {
                        res.Header(header.Key, header.Value);
                    }
                }

                if (string.IsNullOrEmpty(Body))
                {
                    res.With(Status ?? HttpStatusCode.NoContent);
                    return;
                }

                var content = new StringContent(Body);
                if (string.IsNullOrEmpty(Media))
                {
                    try
                    {
                        _ = JsonDocument.Parse(Body);
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json);
                    }
                    catch
                    {
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Text.Plain);
                    }
                }
                else
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(Media);

                res.With(content, Status ?? HttpStatusCode.OK);
            }
        }
    }
}