// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="IResult"/> test assert helper; specifically the <see cref="ToHttpResponseMessageAssertor(HttpRequest?)"/>.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="result">The <see cref="IResult"/>.</param>
    /// <param name="logs">The logs captured during execution.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class HttpResultAssertor(TesterBase owner, IResult result, IEnumerable<string?>? logs, Exception? exception) : AssertorBase<HttpResultAssertor>(owner, logs, exception)
    {
        /// <summary>
        /// Gets the <see cref="IResult"/>.
        /// </summary>
        public IResult Result { get; } = result;

        /// <summary>
        /// Converts the <see cref="IResult"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <param name="httpRequest">The optional requesting <see cref="HttpRequest"/> with <see cref="HttpContext"/>; otherwise, will default.</param>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        /// <remarks>Executes the returned result in a fresh capture buffer and restores the original response stream afterward, including on failure.
        /// Does not include previously written bytes; use <see cref="AssertorBase{TSelf}.SnapshotHttpResponse"/> for an already-written response.</remarks>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor(HttpRequest? httpRequest = null) => ToHttpResponseMessageAssertor(Owner, Result, LogMessages, httpRequest);

        /// <summary>
        /// Converts the <see cref="ValueAssertor{TValue}"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="result">The <see cref="IResult"/> to convert.</param>
        /// <param name="logs">The log messages captured during execution.</param>
        /// <param name="httpRequest">The optional requesting <see cref="HttpRequest"/> with <see cref="HttpContext"/>; otherwise, will default.</param>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        internal static HttpResponseMessageAssertor ToHttpResponseMessageAssertor(TesterBase owner, IResult result, IEnumerable<string?>? logs, HttpRequest? httpRequest)
            => HttpResponseMessageAssertor.ExecuteResult(owner, logs, httpRequest, result.ExecuteAsync);
    }
}
