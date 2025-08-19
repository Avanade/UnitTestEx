// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

#if NET7_0_OR_GREATER

using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="IResult"/> test assert helper; specifically the <see cref="ToHttpResponseMessageAssertor(HttpRequest?)"/>.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="result">The <see cref="IResult"/>.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class HttpResultAssertor(TesterBase owner, IResult result, Exception? exception) : AssertorBase<HttpResultAssertor>(owner, exception)
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
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor(HttpRequest? httpRequest = null) => ToHttpResponseMessageAssertor(Owner, Result, httpRequest);

        /// <summary>
        /// Converts the <see cref="ValueAssertor{TValue}"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="result">The <see cref="IResult"/> to convert.</param>
        /// <param name="httpRequest">The optional requesting <see cref="HttpRequest"/>; otherwise, will default.</param>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        internal static HttpResponseMessageAssertor ToHttpResponseMessageAssertor(TesterBase owner, IResult result, HttpRequest? httpRequest)
        {
            var sw = Stopwatch.StartNew();
            using var ms = new MemoryStream();
            var context = httpRequest?.HttpContext ?? new DefaultHttpContext { RequestServices = owner.Services };
            context.Response.Body = ms;

            result.ExecuteAsync(context).GetAwaiter().GetResult();

            var hr = new HttpResponseMessage((System.Net.HttpStatusCode)context.Response.StatusCode);
            foreach (var h in context.Response.Headers)
                hr.Headers.TryAddWithoutValidation(h.Key, [.. h.Value]);

            ms.Position = 0;
            hr.Content = new ByteArrayContent(ms.ToArray());

            hr.Content.Headers.ContentLength = context.Response.ContentLength;
            if (context.Response.ContentType is not null && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Response.ContentType, out var ct))
                hr.Content.Headers.ContentType = ct;

            sw.Stop();
            owner.LogHttpResponseMessage(hr, sw);

            return new HttpResponseMessageAssertor(owner, hr);
        }
    }
}

#endif