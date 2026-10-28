// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides a unit-testing <see cref="HttpRequestData"/> (isolated worker) implementation that wraps an underlying <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
    /// </summary>
    /// <remarks>The <see cref="HttpRequest"/> <see cref="HttpContext"/> is also added to the <see cref="FunctionContext.Items"/> (key '<c>HttpRequestContext</c>') to mirror the ASP.NET Core integration; i.e. enables <c>FunctionContext.GetHttpContext()</c>.</remarks>
    public class TestHttpRequestData : HttpRequestData
    {
        internal const string HttpContextKey = "HttpRequestContext";

        /// <summary>
        /// Initializes a new instance of the <see cref="TestHttpRequestData"/> class.
        /// </summary>
        /// <param name="httpRequest">The underlying <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.</param>
        /// <param name="functionContext">The <see cref="FunctionContext"/>.</param>
        public TestHttpRequestData(HttpRequest httpRequest, FunctionContext functionContext) : base(functionContext)
        {
            HttpRequest = httpRequest ?? throw new ArgumentNullException(nameof(httpRequest));
            functionContext.Items[HttpContextKey] = httpRequest.HttpContext;

            Headers = [];
            foreach (var h in httpRequest.Headers)
                Headers.TryAddWithoutValidation(h.Key, h.Value.Where(v => v is not null).Select(v => v!));

            Cookies = [.. httpRequest.Cookies.Select(c => new HttpCookie(c.Key, c.Value))];
            Url = new Uri(httpRequest.GetEncodedUrl());
        }

        /// <summary>
        /// Gets the underlying <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
        /// </summary>
        public HttpRequest HttpRequest { get; }

        /// <inheritdoc/>
        public override Stream Body => HttpRequest.Body;

        /// <inheritdoc/>
        public override HttpHeadersCollection Headers { get; }

        /// <inheritdoc/>
        public override IReadOnlyCollection<IHttpCookie> Cookies { get; }

        /// <inheritdoc/>
        public override Uri Url { get; }

        /// <inheritdoc/>
        public override IEnumerable<ClaimsIdentity> Identities => HttpRequest.HttpContext.User?.Identities ?? [];

        /// <inheritdoc/>
        public override string Method => HttpRequest.Method;

        /// <inheritdoc/>
        public override HttpResponseData CreateResponse() => new TestHttpResponseData(FunctionContext);
    }
}
