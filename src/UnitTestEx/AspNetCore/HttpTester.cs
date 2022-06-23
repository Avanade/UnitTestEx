// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using Ceh = CoreEx.Http;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides <see cref="HttpRequestMessage"/> send testing.
    /// </summary>
    public class HttpTester : HttpTesterBase<HttpTester>
    {
        /// <summary>
        /// Initializes a new <see cref="HttpTester"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTester(TesterBase owner, TestServer testServer) : base(owner, testServer) { }

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions = null)
            => RunAsync(httpMethod, requestUri, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, string? body, string? contentType = MediaTypeNames.Text.Plain)
            => RunAsync(httpMethod, requestUri, body, null, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, string? body, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunAsync(httpMethod, requestUri, body, requestOptions, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with JSON serialized <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The request body value.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<T>(HttpMethod httpMethod, string? requestUri, T? value, Ceh.HttpRequestOptions? requestOptions = null)
            => SendAsync(httpMethod, requestUri, value, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions = null)
            => SendAsync(httpMethod, requestUri, requestOptions);

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, string? body, string? contentType = MediaTypeNames.Text.Plain)
            => SendAsync(httpMethod, requestUri, body, contentType, null);

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, string? body, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => SendAsync(httpMethod, requestUri, body, contentType, requestOptions);

        /// <summary>
        /// Runs the test by sending an <see cref="HttpRequestMessage"/> with JSON serialized <paramref name="value"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The request body value.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<T>(HttpMethod httpMethod, string? requestUri, T? value, Ceh.HttpRequestOptions? requestOptions = null)
            => SendAsync(httpMethod, requestUri, value, requestOptions);
    }
}