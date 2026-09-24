// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Reflection;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents a single response within an <see cref="IHttpMockResponseSequence"/>.
    /// </summary>
    /// <remarks>Common to both Tier 1's <see cref="MockHttpClientResponse"/> (reused for this role) and Tier 2/3's <c>AspireHttpMockResponseSequenceItem</c>. Unlike <see cref="IHttpMockResponse"/>'s
    /// terminal members, these are synchronous on both tiers - the underlying mapping/mock is only applied once the whole sequence has been configured.
    /// <para>This is deliberately a minimal, irreducible core of abstract members - the remaining convenience overloads (e.g. <c>Headers(...)</c>, an integer-<c>Delay</c>, a no-body
    /// <c>With</c>, a raw-JSON-string <c>WithJson</c>, or the JSON-embedded-resource variants) are provided as C# default interface methods, composed purely from the core members below,
    /// so concrete implementations (i.e. each tier) don't need to reimplement them.</para></remarks>
    public interface IHttpMockResponseSequenceItem
    {
        /// <summary>
        /// Adds the specified response header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="IHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponseSequenceItem Header(string name, string value);

        /// <summary>
        /// Delays the response by the specified <paramref name="timeSpan"/>.
        /// </summary>
        /// <param name="timeSpan">The delay.</param>
        /// <returns>The <see cref="IHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponseSequenceItem Delay(TimeSpan timeSpan);

        /// <summary>
        /// Sets the response with the specified (optional) <paramref name="content"/>, <paramref name="statusCode"/> and <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="content">The response body content; where <c>null</c> the response will have no body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="mediaType">The response content media type (ignored where <paramref name="content"/> is <c>null</c>); defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        void With(string? content, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = MediaTypeNames.Text.Plain);

        /// <summary>
        /// Sets the response with the JSON serialized representation of the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize as the response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <remarks>Kept as a core (non-default) member, rather than composed from <see cref="With(string?, HttpStatusCode, string)"/>, because Tier 1 serializes <paramref name="value"/>
        /// using its own configurable <c>IJsonSerializer</c> (which may not be <c>System.Text.Json</c>); a shared default implementation could not honour that.</remarks>
        void WithJson<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK);

        /// <summary>
        /// Adds the specified response <paramref name="headers"/>.
        /// </summary>
        /// <param name="headers">The header name/value pairs.</param>
        /// <returns>The <see cref="IHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponseSequenceItem Headers(IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var header in headers ?? throw new ArgumentNullException(nameof(headers)))
                Header(header.Key, header.Value);

            return this;
        }

        /// <summary>
        /// Delays the response by the specified number of <paramref name="milliseconds"/>.
        /// </summary>
        /// <param name="milliseconds">The delay in milliseconds.</param>
        /// <returns>The <see cref="IHttpMockResponseSequenceItem"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponseSequenceItem Delay(int milliseconds) => Delay(TimeSpan.FromMilliseconds(milliseconds));

        /// <summary>
        /// Sets the response with no body and the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        void With(HttpStatusCode statusCode = HttpStatusCode.OK) => With(null, statusCode, MediaTypeNames.Text.Plain);

        /// <summary>
        /// Sets the response with the specified raw <paramref name="json"/> content.
        /// </summary>
        /// <param name="json">The raw JSON response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        void WithJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) => With(json ?? throw new ArgumentNullException(nameof(json)), statusCode, MediaTypeNames.Application.Json);

        /// <summary>
        /// Sets the response with the JSON content of the named embedded resource within the calling <see cref="Assembly"/>.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        void WithJsonResource<TAssembly>(string resourceName, HttpStatusCode statusCode = HttpStatusCode.OK) => WithJsonResource(resourceName, typeof(TAssembly).Assembly, statusCode);

        /// <summary>
        /// Sets the response with the JSON content of the named embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to the calling <see cref="Assembly"/> where not specified.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        void WithJsonResource(string resourceName, Assembly? assembly = null, HttpStatusCode statusCode = HttpStatusCode.OK) => WithJson(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), statusCode);
    }
}
