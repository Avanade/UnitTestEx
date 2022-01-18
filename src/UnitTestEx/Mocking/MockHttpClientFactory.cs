// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="IHttpClientFactory"/> mocking.
    /// </summary>
    public class MockHttpClientFactory
    {
        private readonly Dictionary<string, MockHttpClient> _mockClients = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientFactory"/> class.
        /// </summary>
        public MockHttpClientFactory(TestFrameworkImplementor implementor) => Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets or sets the <see cref="ILogger"/>.
        /// </summary>
        internal ILogger? Logger { get; set; }

        /// <summary>
        /// Gets the <see cref="Mock"/> <see cref="IHttpClientFactory"/>.
        /// </summary>
        public Mock<IHttpClientFactory> HttpClientFactory { get; } = new Mock<IHttpClientFactory>();

        /// <summary>
        /// Creates the <see cref="MockHttpClient"/> with the specified logical <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The logical name of the client.</param>
        /// <param name="baseAddress">The base address of Uniform Resource Identifier (URI) of the Internet resource used when sending requests.</param>
        /// <returns>The <see cref="MockHttpClient"/>.</returns>
        /// <remarks>Only a single client can be created per logical name.</remarks>
        public MockHttpClient CreateClient(string name, Uri baseAddress)
        {
            if (_mockClients.ContainsKey(name ?? throw new ArgumentNullException(nameof(name))))
                throw new ArgumentException("This named client has already been defined.", nameof(name));

            var mc = new MockHttpClient(this, name, baseAddress);
            _mockClients.Add(name, mc);
            return mc;
        }

        /// <summary>
        /// Creates the <see cref="MockHttpClient"/> with the specified logical <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The logical name of the client.</param>
        /// <param name="baseAddress">The base address of Uniform Resource Identifier (URI) of the Internet resource used when sending requests; defaults to '<c>https://unittest</c>' where not specified.</param>
        /// <returns>The <see cref="MockHttpClient"/>.</returns>
        /// <remarks>Only a single client can be created per logical name.</remarks>
        public MockHttpClient CreateClient(string name, string? baseAddress = null)
        {
            if (_mockClients.ContainsKey(name ?? throw new ArgumentNullException(nameof(name))))
                throw new ArgumentException("This named client has already been defined.", nameof(name));

            var mc = new MockHttpClient(this, name, baseAddress == null ? null : new Uri(baseAddress));
            _mockClients.Add(name, mc);
            return mc;
        }

        /// <summary>
        /// Replaces (or adds) the singleton <see cref="IHttpClientFactory"/> within the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</returns>
        public IServiceCollection Replace(IServiceCollection sc) => sc.ReplaceSingleton(sp =>
        {
            Logger = sp.GetService<ILogger<MockHttpClientFactory>>();
            Logger.LogInformation($"Replacing '{nameof(HttpClientFactory)}' service provider (DI) instance with '{nameof(MockHttpClientFactory)}'.");
            return HttpClientFactory.Object;
        });

        /// <summary>
        /// Gets the logically named mocked <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="name">The logical name of the client.</param>
        /// <returns>The <see cref="HttpClient"/> where it exists; otherwise; <c>null</c>.</returns>
        public HttpClient? GetHttpClient(string name) => _mockClients.GetValueOrDefault(name ?? throw new ArgumentNullException(nameof(name)))?.HttpClient;

        /// <summary>
        /// Verifies that all verifiable <see cref="Mock"/> expectations have been met for all <see cref="MockHttpClient"/> instances; being all requests have been invoked.
        /// </summary>
        /// <remarks>This invokes <see cref="MockHttpClient.Verify"/> for each <see cref="MockHttpClient"/> instance.</remarks>
        public void VerifyAll()
        {
            foreach (var mc in _mockClients)
            {
                mc.Value.Verify();
            }
        }
    }
}