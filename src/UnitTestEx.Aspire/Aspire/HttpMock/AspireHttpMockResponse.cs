// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Mocking;
using WireMock.Admin.Mappings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Provides the stubbed response configuration for an <see cref="AspireHttpMockRequest"/>.
    /// </summary>
    /// <remarks>Unlike Tier 1's <see cref="Mocking.MockHttpClientResponse"/>, the terminal <c>With*</c> methods here are asynchronous (see <see cref="AspireHttpMockClient"/> remarks) as they perform
    /// a real HTTP POST to the underlying WireMock.Net server resource's admin API to register the mapping; they must be awaited.
    /// <para>Implements the shared <see cref="IHttpMockResponse"/> abstraction (see that type's remarks) so that test-authoring code can be written once against the interface and
    /// reused identically regardless of which tier applied it.</para></remarks>
    public sealed class AspireHttpMockResponse : IHttpMockResponse
    {
        private readonly AspireHttpMockRequest _request;
        private readonly ResponseModel _response = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockResponse"/> class.
        /// </summary>
        /// <param name="request">The <see cref="AspireHttpMockRequest"/>.</param>
        internal AspireHttpMockResponse(AspireHttpMockRequest request) => _request = request;

        /// <summary>
        /// Adds the specified response header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="AspireHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponse Header(string name, string value)
        {
            ResponseModelHelper.Header(_response, name, value);
            return this;
        }

        /// <summary>
        /// Adds the specified response headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="AspireHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponse Headers(IEnumerable<KeyValuePair<string, string>> headers)
        {
            ResponseModelHelper.Headers(_response, headers);
            return this;
        }

        /// <summary>
        /// Delays the response by the specified <paramref name="milliseconds"/>.
        /// </summary>
        /// <param name="milliseconds">The delay in milliseconds.</param>
        /// <returns>The <see cref="AspireHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponse Delay(int milliseconds)
        {
            _response.Delay = milliseconds;
            return this;
        }

        /// <summary>
        /// Delays the response by the specified <paramref name="timeSpan"/>.
        /// </summary>
        /// <param name="timeSpan">The delay.</param>
        /// <returns>The <see cref="AspireHttpMockResponse"/> to support fluent-style method-chaining.</returns>
        public AspireHttpMockResponse Delay(TimeSpan timeSpan) => Delay((int)timeSpan.TotalMilliseconds);

        /// <summary>
        /// Stubs the response with the specified <paramref name="statusCode"/> and no body.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithAsync(HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
        {
            ResponseModelHelper.With(_response, statusCode);
            return ApplyAsync(cancellationToken);
        }

        /// <summary>
        /// Stubs the response with the specified <paramref name="content"/>, <paramref name="statusCode"/> and <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="content">The response body content.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="mediaType">The response content media type; defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithAsync(string content, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = MediaTypeNames.Text.Plain, CancellationToken cancellationToken = default)
        {
            ResponseModelHelper.With(_response, content, statusCode, mediaType);
            return ApplyAsync(cancellationToken);
        }

        /// <summary>
        /// Stubs the response with the specified <paramref name="json"/> body.
        /// </summary>
        /// <param name="json">The JSON response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithJsonAsync([StringSyntax(StringSyntaxAttribute.Json)] string json, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithAsync(json, statusCode, MediaTypeNames.Application.Json, cancellationToken);

        /// <summary>
        /// Stubs the response with the JSON serialized representation of the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize as the response body.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithJsonAsync<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithJsonAsync(JsonSerializer.Serialize(value), statusCode, cancellationToken);

        /// <summary>
        /// Stubs the response with the JSON formatted embedded resource content.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithJsonResourceAsync<TAssembly>(string resourceName, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithJsonResourceAsync(resourceName, typeof(TAssembly).Assembly, statusCode, cancellationToken);

        /// <summary>
        /// Stubs the response with the JSON formatted embedded resource content.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>; defaults to <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        public Task<AspireHttpMockedRequest> WithJsonResourceAsync(string resourceName, Assembly? assembly = null, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
            => WithJsonAsync(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), statusCode, cancellationToken);

        /// <summary>
        /// Provides the means to mock a sequence of one or more responses for the owning <see cref="AspireHttpMockRequest"/>; each subsequent matching invocation of the request will
        /// return the next response in the sequence. Exactly one invocation per configured response is expected: any additional invocation beyond the configured responses will
        /// itself be met with a distinct <see cref="HttpStatusCode.InternalServerError"/> "exhausted" response (mirroring Tier 1's <see cref="Mocking.MockHttpClientResponse.WithSequence"/>,
        /// which throws synchronously on an unexpected additional invocation).
        /// </summary>
        /// <param name="sequence">The action to enable the addition of one or more responses (see <see cref="AspireHttpMockResponseSequence.Respond"/>).</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation (across the whole sequence) after the fact.</returns>
        /// <exception cref="InvalidOperationException">Thrown where <see cref="AspireHttpMockRequest.Times(Times)"/> has already been configured; mirroring Tier 1, a sequence's own
        /// completeness (exactly one invocation per configured response) <i>is</i> the expectation, so an explicit <see cref="Times"/> cannot also be specified.</exception>
        /// <remarks>Internally this is implemented using WireMock.Net's Scenario/state mechanism (see the <c>Scenario</c>, <c>WhenStateIs</c> and <c>SetStateTo</c> properties of
        /// <see cref="MappingModel"/>): one mapping per response is posted, plus a final "exhausted" guard mapping, all chained together via a scenario unique to this sequence, so they
        /// cannot clash with other stubs or tests sharing the same underlying WireMock.Net server resource.</remarks>
        public async Task<AspireHttpMockedRequest> WithSequenceAsync(Action<AspireHttpMockResponseSequence> sequence, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sequence);

            if (_request.ExpectedTimes != null)
                throw new InvalidOperationException($"{nameof(AspireHttpMockRequest)}.{nameof(AspireHttpMockRequest.Times)} cannot be combined with {nameof(WithSequenceAsync)}; the sequence's own completeness (exactly one invocation per configured response, no more) is the implicit expectation, mirroring Tier 1's {nameof(Mocking.MockHttpClientResponse)}.{nameof(Mocking.MockHttpClientResponse.WithSequence)}.");

            var seq = new AspireHttpMockResponseSequence();
            sequence(seq);

            if (seq.Responses.Count == 0)
                throw new InvalidOperationException($"At least one response must be added via {nameof(AspireHttpMockResponseSequence)}.{nameof(AspireHttpMockResponseSequence.Respond)} within the sequence action.");

            var count = seq.Responses.Count;
            var scenario = Guid.NewGuid().ToString("N");
            var mappings = new List<MappingModel>(count + 1);

            for (var i = 0; i < count; i++)
            {
                mappings.Add(new MappingModel
                {
                    Request = _request.Rule,
                    Response = seq.Responses[i],
                    Scenario = scenario,
                    WhenStateIs = i == 0 ? null : i.ToString(CultureInfo.InvariantCulture),
                    // Note: always advances - even the final configured response transitions onward into the "exhausted" guard state below, rather than looping back on itself; this is
                    // what turns the sequence length into an exact (not "at least") expectation, mirroring Tier 1's exhaustion behaviour.
                    SetStateTo = (i + 1).ToString(CultureInfo.InvariantCulture)
                });
            }

            // The final "exhausted" guard mapping: matches once every configured response has been consumed exactly once, and self-loops so any further invocation keeps hitting it.
            var exhaustedResponse = new ResponseModel();
            ResponseModelHelper.WithJson(exhaustedResponse, JsonSerializer.Serialize(new { error = $"There were {count} response(s) configured for the sequence and these have been exhausted; i.e. an unexpected additional invocation has occurred." }), HttpStatusCode.InternalServerError);
            mappings.Add(new MappingModel
            {
                Request = _request.Rule,
                Response = exhaustedResponse,
                Scenario = scenario,
                WhenStateIs = count.ToString(CultureInfo.InvariantCulture),
                SetStateTo = count.ToString(CultureInfo.InvariantCulture)
            });

            return await _request.Client.ApplySequenceAsync(mappings, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Posts the built-up <see cref="ResponseModel"/> (with the <see cref="AspireHttpMockRequest.Rule"/>) to the underlying WireMock.Net server resource.
        /// </summary>
        private Task<AspireHttpMockedRequest> ApplyAsync(CancellationToken cancellationToken)
        {
            var mapping = new MappingModel { Request = _request.Rule, Response = _response };
            return _request.Client.ApplyAsync(mapping, _request.ExpectedTimes, cancellationToken);
        }

        /// <inheritdoc/>
        IHttpMockResponse IHttpMockResponse.Header(string name, string value) => Header(name, value);

        /// <inheritdoc/>
        IHttpMockResponse IHttpMockResponse.Delay(TimeSpan timeSpan) => Delay(timeSpan);

        /// <inheritdoc/>
        async Task<IHttpMockedRequest> IHttpMockResponse.WithAsync(string? content, HttpStatusCode statusCode, string mediaType, CancellationToken cancellationToken)
            => content is null ? await WithAsync(statusCode, cancellationToken).ConfigureAwait(false) : await WithAsync(content, statusCode, mediaType, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        async Task<IHttpMockedRequest> IHttpMockResponse.WithJsonAsync<T>(T value, HttpStatusCode statusCode, CancellationToken cancellationToken) => await WithJsonAsync(value, statusCode, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        async Task<IHttpMockedRequest> IHttpMockResponse.WithSequenceAsync(Action<IHttpMockResponseSequence> sequence, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(sequence);
            return await WithSequenceAsync(s => sequence(s), cancellationToken).ConfigureAwait(false);
        }
    }
}
