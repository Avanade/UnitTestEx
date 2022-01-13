// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System;
using System.Net.Http;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="System.Net.Http.HttpClient"/> (more specifically <see cref="HttpMessageHandler"/>) mocking.
    /// </summary>
    public class MockHttpClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClient"/> class.
        /// </summary>
        /// <param name="factory">The <see cref="MockHttpClientFactory"/>.</param>
        /// <param name="name">The logical name of the client.</param>
        /// <param name="baseAddress">The base Uniform Resource Identifier (URI) of the Internet resource used when sending requests.</param>
        internal MockHttpClient(MockHttpClientFactory factory, string name, Uri? baseAddress)
        {
            Factory = factory;
            HttpClient = new HttpClient(new MockHttpClientHandler(factory.Implementor, MessageHandler.Object)) { BaseAddress = baseAddress ?? new Uri("https://unittest") };
            Factory.HttpClientFactory.Setup(x => x.CreateClient(It.Is<string>(x => x == name))).Returns(() => HttpClient);
        }

        /// <summary>
        /// Gets the <see cref="MockHttpClientFactory"/>.
        /// </summary>
        internal MockHttpClientFactory Factory { get; }

        /// <summary>
        /// Gets the <see cref="Mock"/> <see cref="HttpMessageHandler"/>.
        /// </summary>
        public Mock<HttpMessageHandler> MessageHandler { get; } = new Mock<HttpMessageHandler>();

        /// <summary>
        /// Verifies that all verifiable <see cref="Mock"/> expectations have been met; being all requests have been invoked.
        /// </summary>
        /// <remarks>This is a wrapper for '<c>MessageHandler.Verify()</c>' which can be invoked directly to leverage additional capabilities (overloads).</remarks>
        public void Verify() => MessageHandler.Verify();

        /// <summary>
        /// Gets the mocked <see cref="HttpClient"/>.
        /// </summary>
        internal HttpClient HttpClient { get; set; }

        /// <summary>
        /// Creates a new <see cref="MockHttpClientRequest"/> for the <see cref="HttpClient"/> with no body content.
        /// </summary>
        /// <param name="method">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <returns>The <see cref="MockHttpClientRequest"/>.</returns>
        public MockHttpClientRequest Request(HttpMethod method, string requestUri) => new(this, method, requestUri);
    }
}