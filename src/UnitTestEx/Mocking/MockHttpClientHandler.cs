// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides a <see cref="HttpMessageHandler"/> that will log success and failure.
    /// </summary>
    public class MockHttpClientHandler : DelegatingHandler
    {
        private readonly MockHttpClientFactory _factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientHandler"/> class.
        /// </summary>
        /// <param name="factory">The <see cref="MockHttpClientFactory"/>.</param>
        /// <param name="inner">The <see cref="HttpMessageHandler"/>.</param>
        internal MockHttpClientHandler(MockHttpClientFactory factory, HttpMessageHandler inner) : base(inner) => _factory = factory;

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var logger = _factory.Logger ?? _factory.Implementor.CreateLogger(nameof(MockHttpClientFactory));
            logger.LogInformation($"Sending HTTP request {request.Method} {request.RequestUri} {LogContent(request.Content)}");

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response == null)
                throw new MockHttpClientException($"No corresponding MockHttpClient response found for HTTP request {request.Method} {request.RequestUri} {LogContent(request.Content)}");

            logger.LogInformation($"Received HTTP response {response.StatusCode} ({(int)response.StatusCode}) {LogContent(response.Content)}");
            return response;
        }

        /// <summary>
        /// Logs (and formats) the content.
        /// </summary>
        private static string LogContent(HttpContent? content) => content == null ? "No content." : $"{content.ReadAsStringAsync().GetAwaiter().GetResult()} ({content?.Headers?.ContentType?.MediaType ?? "?"})";
    }
}