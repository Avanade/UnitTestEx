// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides <see cref="HttpRequestMessage"/> send testing.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="testServer">The <see cref="TestServer"/>.</param>
    public class HttpTester(TesterBase owner, TestServer testServer) : HttpTesterBase<HttpTester>(owner, testServer)
    {
        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, Action<HttpRequestMessage>? requestModifier = null)
            => RunAsync(httpMethod, requestUri, requestModifier).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, string? body, string? contentType = MediaTypeNames.Text.Plain, Action<HttpRequestMessage>? requestModifier = null)
            => RunAsync(httpMethod, requestUri, body, contentType, requestModifier).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with JSON serialized <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The request body value.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<T>(HttpMethod httpMethod, string? requestUri, T? value, Action<HttpRequestMessage>? requestModifier = null)
            => SendAsync(httpMethod, requestUri, value, requestModifier).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, Action<HttpRequestMessage>? requestModifier = null)
            => SendAsync(httpMethod, requestUri, requestModifier);

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, string? body, string? contentType = MediaTypeNames.Text.Plain, Action<HttpRequestMessage>? requestModifier = null)
            => SendAsync(httpMethod, requestUri, body, contentType, requestModifier);

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with JSON serialized <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The request body value.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequestMessage"/> modifier.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<T>(HttpMethod httpMethod, string? requestUri, T? value, Action<HttpRequestMessage>? requestModifier = null)
            => SendAsync(httpMethod, requestUri, value, requestModifier);
    }
}