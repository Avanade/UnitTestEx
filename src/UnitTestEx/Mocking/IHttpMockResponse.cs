// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the stubbed response configuration for an <see cref="IHttpMockRequest"/>.
    /// </summary>
    /// <remarks>Common to both Tier 1's <see cref="MockHttpClientResponse"/> and Tier 2/3's <c>AspireHttpMockResponse</c>. The terminal <c>With*Async</c> members are asynchronous on
    /// <b>both</b> tiers here (unlike their tier-native equivalents, which are synchronous on Tier 1): Tier 2/3 genuinely performs real HTTP I/O (a POST to the WireMock.Net server
    /// resource's admin API) to apply the mapping, so there is no honest way to offer a synchronous member on this shared surface. Tier 1's own native members remain synchronous for
    /// direct (non-interface) use; the <c>*Async</c> members here perform the identical synchronous configuration and wrap the result, so they are safe (and cheap) to await on Tier 1 too.
    /// <para>This is deliberately a minimal, irreducible core of abstract members - the remaining convenience overloads (e.g. <c>Headers(...)</c>, an integer-<c>Delay</c>, a no-body
    /// <c>WithAsync</c>, a raw-JSON-string <c>WithJsonAsync</c>, or the JSON-embedded-resource variants) are provided as C# default interface methods, composed purely from the core
    /// members below, so concrete implementations (i.e. each tier) don't need to reimplement them.</para></remarks>
    public interface IHttpMockResponse
    {
        /// <summary>
        /// Adds the specified response header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="IHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponse Header(string name, string value);

        /// <summary>
        /// Delays the response by the specified <paramref name="timeSpan"/>.
        /// </summary>
        /// <param name="timeSpan">The delay.</param>
        /// <returns>The <see cref="IHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponse Delay(TimeSpan timeSpan);

        /// <summary>
        /// Stubs the response with the specified (optional) <paramref name="content"/>, <paramref name="statusCode"/> and <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="content">The response body content; where <c>null</c> the response will have no body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="mediaType">The response content media type (ignored where <paramref name="content"/> is <c>null</c>); defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        Task<IHttpMockedRequest> WithAsync(string? content, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = MediaTypeNames.Text.Plain, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stubs the response with the JSON serialized representation of the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize as the response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        /// <remarks>Kept as a core (non-extension) member, rather than composed from <see cref="WithAsync(string?, HttpStatusCode, string, CancellationToken)"/>, because Tier 1 serializes
        /// <paramref name="value"/> using its own configurable <c>IJsonSerializer</c> (which may not be <c>System.Text.Json</c>); a shared extension method could not honour that.</remarks>
        Task<IHttpMockedRequest> WithJsonAsync<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default);

        /// <summary>
        /// Provides the means to mock a sequence of one or more responses for the owning <see cref="IHttpMockRequest"/>; each subsequent matching invocation of the request will
        /// return the next response in the sequence.
        /// </summary>
        /// <param name="sequence">The action to enable the addition of one or more responses (see <see cref="IHttpMockResponseSequence.Respond"/>).</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation (across the whole sequence) after the fact.</returns>
        /// <remarks>The exact exhaustion semantics (what happens on an invocation beyond the configured responses) are tier-specific - see the native <c>WithSequence</c>/<c>WithSequenceAsync</c>
        /// documentation on the concrete implementation being used.</remarks>
        Task<IHttpMockedRequest> WithSequenceAsync(Action<IHttpMockResponseSequence> sequence, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds the specified response <paramref name="headers"/>.
        /// </summary>
        /// <param name="headers">The header name/value pairs.</param>
        /// <returns>The <see cref="IHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponse Headers(IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var header in headers ?? throw new ArgumentNullException(nameof(headers)))
                Header(header.Key, header.Value);

            return this;
        }

        /// <summary>
        /// Delays the response by the specified number of <paramref name="milliseconds"/>.
        /// </summary>
        /// <param name="milliseconds">The delay in milliseconds.</param>
        /// <returns>The <see cref="IHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        IHttpMockResponse Delay(int milliseconds) => Delay(TimeSpan.FromMilliseconds(milliseconds));

        /// <summary>
        /// Stubs the response with no body and the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        Task<IHttpMockedRequest> WithAsync(HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default) => WithAsync(null, statusCode, MediaTypeNames.Text.Plain, cancellationToken);

        /// <summary>
        /// Stubs the response with the specified raw <paramref name="json"/> content.
        /// </summary>
        /// <param name="json">The raw JSON response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        Task<IHttpMockedRequest> WithJsonAsync(string json, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithAsync(json ?? throw new ArgumentNullException(nameof(json)), statusCode, MediaTypeNames.Application.Json, cancellationToken);

        /// <summary>
        /// Stubs the response with the JSON content of the named embedded resource within the calling <see cref="Assembly"/>.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        Task<IHttpMockedRequest> WithJsonResourceAsync<TAssembly>(string resourceName, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithJsonResourceAsync(resourceName, typeof(TAssembly).Assembly, statusCode, cancellationToken);

        /// <summary>
        /// Stubs the response with the JSON content of the named embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to the calling <see cref="Assembly"/> where not specified.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        Task<IHttpMockedRequest> WithJsonResourceAsync(string resourceName, Assembly? assembly = null, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithJsonAsync(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), statusCode, cancellationToken);
    }
}
