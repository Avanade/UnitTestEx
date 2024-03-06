// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="System.Net.Http.HttpClient"/> (more specifically <see cref="HttpMessageHandler"/>) mocking.
    /// </summary>
    public class MockHttpClient
    {
        /// <summary>
        /// Gets the default <see cref="HttpClient.BaseAddress"/> being '<c>https://unittest</c>'.
        /// </summary>
        public static Uri DefaultBaseAddress { get; } = new Uri("https://unittest");

        private readonly Uri? _baseAddress;
        private readonly List<MockHttpClientRequest> _requests = [];
        private readonly Lazy<HttpClient> _httpClient;
        private bool _noMocking;
        private bool _useHttpMessageHandlers;
        private Type[] _excludeTypes = [];
        private bool _useHttpClientConfigurations;

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
            _httpClient = new(CreateHttpClient);
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
        internal HttpClient GetHttpClient() => _httpClient.Value;

        /// <summary>
        /// Create the <see cref="HttpClient"/>.
        /// </summary>
        private HttpClient CreateHttpClient()
        { 
            // Get the factory options where applicable.
            HttpClientFactoryOptions? options = null;
            if (Factory.ServiceProvider is not null)
            {
                var om = Factory.ServiceProvider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
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
                var builder = new MockHttpMessageHandlerBuilder(Name, Factory.ServiceProvider!, _noMocking ? new HttpClientHandler() : new MockHttpClientHandler(Factory, MessageHandler.Object), _excludeTypes);

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
        /// Indicates that the <see cref="HttpMessageHandler"/> and <see cref="GetHttpClient()"/> configurations are to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This is a combination of both <see cref="WithHttpClientConfigurations"/> and <see cref="WithHttpMessageHandlers(Type[])"/>.</remarks>
        public MockHttpClient WithConfigurations(params Type[] excludeTypes) => WithHttpClientConfigurations().WithHttpMessageHandlers(excludeTypes);

        /// <summary>
        /// Indicates that the <see cref="HttpMessageHandler"/> and <see cref="GetHttpClient()"/> configurations are <b>not</b> to be used.
        /// </summary>
        /// <returns>The <see cref="MockHttpClient"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The is a combination of both <see cref="WithoutHttpClientConfigurations"/> and <see cref="WithoutHttpMessageHandlers"/>.</remarks>
        public MockHttpClient WithoutConfigurations() => WithoutHttpClientConfigurations().WithoutHttpMessageHandlers();

        /// <summary>
        /// Indicates that the <see cref="HttpMessageHandler"/> configurations for the <see cref="GetHttpClient()"/> are to be used.
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
        /// Indicates that the <see cref="HttpMessageHandler"/> configurations for the <see cref="GetHttpClient()"/> are <b>not</b> to be used.
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
        /// Indicates that the configurations for the <see cref="GetHttpClient()"/> aer to be used.
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
        /// Indicates that the configurations for the <see cref="GetHttpClient()"/> are <b>not</b> to be used.
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
        /// Indicates that the <see cref="GetHttpClient()"/> is to be instantiated with <b>no</b> mocking; i.e. will result in an actual/real HTTP request.
        /// </summary>
        /// <param name="excludeTypes">The <see cref="HttpMessageHandler"/> types to be excluded.</param>
        /// <remarks>Once set this is immutable.
        /// <para><i>Note:</i> although this implies that the existing <see cref="GetHttpClient()"/> and related .NET native <see cref="IHttpClientFactory"/> implementation is being leveraged, this is not the case. This is still using the <see cref="MockHttpClientFactory"/>
        /// and related internal <see cref="HttpMessageHandlerBuilder"/> implementation. Although, best efforts have been made to achieve like-for-like behavior, some edge cases may not be accounted for; please raise an issue to identify for correction (where possible).</para></remarks>
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
        /// <param name="method">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <returns>The <see cref="MockHttpClientRequest"/>.</returns>
        public MockHttpClientRequest Request(HttpMethod method, string requestUri)
        {
            if (_noMocking)
                throw new InvalidOperationException($"{nameof(Request)} is not supported where {nameof(WithoutMocking)} has been specified.");

            var r = new MockHttpClientRequest(this, method, requestUri);
            _requests.Add(r);
            return r;
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
        /// Provides an internal/mocked <see cref="HttpMessageHandlerBuilder"/>
        /// </summary>
        private class MockHttpMessageHandlerBuilder(string? name, IServiceProvider services, HttpMessageHandler primaryHandler, Type[] excludeTypes) : HttpMessageHandlerBuilder
        {
            private readonly Type[] _excludeTypes = excludeTypes;

            /// <inheritdoc/>
            public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();

            public override IServiceProvider Services { get; } = services;

            /// <inheritdoc/>
            public override string? Name { get; set; } = name;

            /// <inheritdoc/>
            public override HttpMessageHandler PrimaryHandler { get; set; } = primaryHandler;

            /// <inheritdoc/>
            public override HttpMessageHandler Build() => CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers.Where(x => !_excludeTypes.Contains(x.GetType())));
        }
    }
}