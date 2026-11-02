// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides a unit-testing <see cref="HttpResponseData"/> (isolated worker) implementation.
    /// </summary>
    /// <param name="functionContext">The <see cref="FunctionContext"/>.</param>
    public class TestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
    {
        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        /// <inheritdoc/>
        public override HttpHeadersCollection Headers { get; set; } = [];

        /// <inheritdoc/>
        public override Stream Body { get; set; } = new MemoryStream();

        /// <inheritdoc/>
        public override HttpCookies Cookies { get; } = new TestHttpCookies();
    }

    /// <summary>
    /// Provides a unit-testing <see cref="HttpCookies"/> (isolated worker) implementation.
    /// </summary>
    public class TestHttpCookies : HttpCookies
    {
        private readonly List<IHttpCookie> _cookies = [];

        /// <summary>
        /// Gets the appended <see cref="IHttpCookie"/> items.
        /// </summary>
        public IReadOnlyList<IHttpCookie> Items => _cookies;

        /// <inheritdoc/>
        public override void Append(string name, string value) => _cookies.Add(new HttpCookie(name, value));

        /// <inheritdoc/>
        public override void Append(IHttpCookie cookie) => _cookies.Add(cookie ?? throw new ArgumentNullException(nameof(cookie)));

        /// <inheritdoc/>
        public override IHttpCookie CreateNew() => new HttpCookie(string.Empty, string.Empty);
    }
}
