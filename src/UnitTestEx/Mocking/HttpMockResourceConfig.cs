// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the shared (tier-agnostic) YAML/JSON embedded-resource request/response configuration loading used by
    /// <see cref="IHttpMockClient.WithRequestsFromResourceAsync(string, Assembly?, CancellationToken)"/>.
    /// </summary>
    /// <remarks>Uses the same YAML/JSON schema as Tier 1's native, synchronous <see cref="MockHttpClient.WithRequestsFromResource(string, Assembly?)"/> (see <c>src/UnitTestEx/Schema/mock.unittestex.json</c>),
    /// but is implemented purely against the shared <see cref="IHttpMockClient"/>/<see cref="IHttpMockRequest"/>/<see cref="IHttpMockResponse"/>/<see cref="IHttpMockResponseSequenceItem"/> abstractions -
    /// so the same embedded resource works identically against Tier 1's <see cref="MockHttpClient"/> and Tier 2/3's <c>AspireHttpMockClient</c>, without either tier reimplementing the parsing.
    /// <para>One intentional behavioral difference versus Tier 1's native method: where a request entry omits <c>body</c> entirely, Tier 1's native method matches only a request with <i>no</i> body
    /// (content must be absent); this shared implementation instead matches <i>any</i> body (equivalent to <c>body: ^</c>), via <see cref="IHttpMockRequest.WithAnyBody"/> - the shared interface has no
    /// means to express "body must be absent", and this is both consistent with <see cref="IHttpMockRequest.WithAnyBody"/>'s own true-wildcard semantics and almost always what's actually wanted
    /// (e.g. a bodyless <c>GET</c> matches either way).</para>
    /// <para>Response header values are applied via repeated <see cref="IHttpMockResponse.Header(string, string)"/>/<see cref="IHttpMockResponseSequenceItem.Header(string, string)"/> calls, one per
    /// configured value; Tier 1 accumulates multiple values per header name, whereas Aspire's WireMock.Net-backed response currently retains only the last value written for a given header name.</para></remarks>
    internal static class HttpMockResourceConfig
    {
        /// <summary>
        /// Adds mocked request(s) from the named embedded resource (formatted as either YAML or JSON) to the specified <paramref name="client"/>.
        /// </summary>
        /// <param name="client">The <see cref="IHttpMockClient"/> to configure.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name); must end in <c>.yaml</c>, <c>.yml</c>, <c>.json</c> or <c>.jsn</c>.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        public static async Task AddRequestsFromResourceAsync(IHttpMockClient client, string resourceName, Assembly assembly, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(resourceName, nameof(resourceName));

            bool isYaml;
            if (resourceName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || resourceName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                isYaml = true;
            else if (resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || resourceName.EndsWith(".jsn", StringComparison.OrdinalIgnoreCase))
                isYaml = false;
            else
                throw new ArgumentException("Only YAML or JSON embedded resources are supported; the extension must be one of the following: .yaml, .yml, .json, .jsn", nameof(resourceName));

            using var sr = Resource.GetStream(resourceName, assembly);
            var reqs = isYaml ? Resource.DeserializeYaml<List<ConfigRequest>>(sr) : Resource.DeserializeJson<List<ConfigRequest>>(sr);

            if (reqs is not null)
            {
                foreach (var req in reqs)
                    await req.AddAsync(client, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Infers the default media type for the specified <paramref name="body"/> where not explicitly configured: <see cref="MediaTypeNames.Application.Json"/> where it parses as JSON,
        /// otherwise <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        private static string InferMediaType(string body)
        {
            try
            {
                _ = JsonDocument.Parse(body);
                return MediaTypeNames.Application.Json;
            }
            catch (JsonException)
            {
                return MediaTypeNames.Text.Plain;
            }
        }

        /// <summary>
        /// The mocked config contract for a request.
        /// </summary>
        private sealed class ConfigRequest
        {
            public string? Method { get; set; }
            public string? Uri { get; set; }
            public string? Body { get; set; }
            public string? Media { get; set; }
            public string[]? Ignore { get; set; }
            public ConfigResponse? Response { get; set; }
            public List<ConfigResponse>? Sequence { get; set; }

            /// <summary>
            /// Adds the request and response(s) to the <paramref name="client"/>.
            /// </summary>
            public async Task AddAsync(IHttpMockClient client, CancellationToken cancellationToken)
            {
                var request = client.Request(string.IsNullOrEmpty(Method) ? HttpMethod.Get : new HttpMethod(Method.ToUpperInvariant()), Uri);

                IHttpMockRequestBody body;
                if (Body is not null && Body != "^")
                {
                    var media = string.IsNullOrEmpty(Media) ? InferMediaType(Body) : Media;
                    body = string.Equals(media, MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase)
                        ? request.WithJsonBody(Body, Ignore ?? [])
                        : request.WithBody(Body, media);
                }
                else
                    body = request.WithAnyBody(); // Also covers the omitted-body case; see remarks on this class for the resulting (intentional) behavioral difference from Tier 1's native method.

                if (Response is not null && Sequence is not null)
                    throw new InvalidOperationException($"A mocked request can not contain both a {nameof(Response)} and a {nameof(Sequence)} as they are mutually exclusive.");

                // One-to-one response.
                if (Sequence is null)
                {
                    await (Response ?? new()).AddAsync(body.Respond, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // A sequence of responses.
                await body.Respond.WithSequenceAsync(seq =>
                {
                    foreach (var res in Sequence)
                        (res ?? new()).Add(seq.Respond());
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The mocked config contract for a response.
        /// </summary>
        private sealed class ConfigResponse
        {
            public HttpStatusCode? Status { get; set; }
            public string? Body { get; set; }
            public string? Media { get; set; }
            public Dictionary<string, string?[]>? Headers { get; set; }

            /// <summary>
            /// Applies this response to a terminal (single) <paramref name="response"/>.
            /// </summary>
            public async Task AddAsync(IHttpMockResponse response, CancellationToken cancellationToken)
            {
                ApplyHeaders((name, value) => response.Header(name, value));

                if (string.IsNullOrEmpty(Body))
                {
                    await response.WithAsync(Status ?? HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var media = string.IsNullOrEmpty(Media) ? InferMediaType(Body) : Media;
                await response.WithAsync(Body, Status ?? HttpStatusCode.OK, media, cancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// Applies this response to a single <paramref name="item"/> within an <see cref="IHttpMockResponseSequence"/>.
            /// </summary>
            public void Add(IHttpMockResponseSequenceItem item)
            {
                ApplyHeaders((name, value) => item.Header(name, value));

                if (string.IsNullOrEmpty(Body))
                {
                    item.With(Status ?? HttpStatusCode.NoContent);
                    return;
                }

                var media = string.IsNullOrEmpty(Media) ? InferMediaType(Body) : Media;
                item.With(Body, Status ?? HttpStatusCode.OK, media);
            }

            /// <summary>
            /// Applies each configured header name/value pair (one <paramref name="add"/> invocation per value) - see remarks on <see cref="HttpMockResourceConfig"/> regarding the
            /// cross-tier multi-value header discrepancy.
            /// </summary>
            private void ApplyHeaders(Action<string, string> add)
            {
                if (Headers is null)
                    return;

                foreach (var header in Headers)
                {
                    foreach (var value in header.Value ?? [])
                        add(header.Key, value ?? string.Empty);
                }
            }
        }
    }
}
