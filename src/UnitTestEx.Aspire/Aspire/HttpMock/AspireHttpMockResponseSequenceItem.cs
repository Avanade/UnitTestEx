// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using WireMock.Admin.Mappings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Represents a single response within a <see cref="AspireHttpMockResponseSequence"/>.
    /// </summary>
    /// <remarks>Unlike <see cref="AspireHttpMockResponse"/>'s terminal <c>With*Async</c> methods, these configuration methods are synchronous; the underlying mapping is only posted
    /// once the whole sequence has been configured (see <see cref="AspireHttpMockResponse.WithSequenceAsync"/>).</remarks>
    public sealed class AspireHttpMockResponseSequenceItem
    {
        private readonly ResponseModel _response;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockResponseSequenceItem"/> class.
        /// </summary>
        /// <param name="response">The <see cref="ResponseModel"/> being built up.</param>
        internal AspireHttpMockResponseSequenceItem(ResponseModel response) => _response = response;

        /// <summary>
        /// Adds the specified response header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="AspireHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponseSequenceItem Header(string name, string value)
        {
            ResponseModelHelper.Header(_response, name, value);
            return this;
        }

        /// <summary>
        /// Adds the specified response headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="AspireHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponseSequenceItem Headers(IEnumerable<KeyValuePair<string, string>> headers)
        {
            ResponseModelHelper.Headers(_response, headers);
            return this;
        }

        /// <summary>
        /// Delays the response by the specified <paramref name="milliseconds"/>.
        /// </summary>
        /// <param name="milliseconds">The delay in milliseconds.</param>
        /// <returns>The <see cref="AspireHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponseSequenceItem Delay(int milliseconds)
        {
            _response.Delay = milliseconds;
            return this;
        }

        /// <summary>
        /// Delays the response by the specified <paramref name="timeSpan"/>.
        /// </summary>
        /// <param name="timeSpan">The delay.</param>
        /// <returns>The <see cref="AspireHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponseSequenceItem Delay(TimeSpan timeSpan) => Delay((int)timeSpan.TotalMilliseconds);

        /// <summary>
        /// Sets the response with the specified <paramref name="statusCode"/> and no body.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        public void With(HttpStatusCode statusCode = HttpStatusCode.OK) => ResponseModelHelper.With(_response, statusCode);

        /// <summary>
        /// Sets the response with the specified <paramref name="content"/>, <paramref name="statusCode"/> and <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="content">The response body content.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="mediaType">The response content media type; defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        public void With(string content, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = MediaTypeNames.Text.Plain) => ResponseModelHelper.With(_response, content, statusCode, mediaType);

        /// <summary>
        /// Sets the response with the specified <paramref name="json"/> body.
        /// </summary>
        /// <param name="json">The JSON response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        public void WithJson([StringSyntax(StringSyntaxAttribute.Json)] string json, HttpStatusCode statusCode = HttpStatusCode.OK) => ResponseModelHelper.WithJson(_response, json, statusCode);

        /// <summary>
        /// Sets the response with the JSON serialized representation of the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize as the response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        public void WithJson<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK) => WithJson(JsonSerializer.Serialize(value), statusCode);

        /// <summary>
        /// Sets the response with the JSON formatted embedded resource content.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        public void WithJsonResource<TAssembly>(string resourceName, HttpStatusCode statusCode = HttpStatusCode.OK) => WithJsonResource(resourceName, typeof(TAssembly).Assembly, statusCode);

        /// <summary>
        /// Sets the response with the JSON formatted embedded resource content.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        public void WithJsonResource(string resourceName, Assembly? assembly = null, HttpStatusCode statusCode = HttpStatusCode.OK) => WithJson(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), statusCode);
    }
}
