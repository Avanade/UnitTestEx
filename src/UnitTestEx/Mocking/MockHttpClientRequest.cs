﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using Moq.Protected;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="HttpRequestMessage"/> configuration for mocking.
    /// </summary>
    public sealed class MockHttpClientRequest
    {
        private readonly MockHttpClient _client;
        private readonly HttpMethod _method;
        private readonly string? _requestUri;
        private bool _anyContent;
        private object? _content;
        private string? _mediaType;
        private string[] _pathsToIgnore = [];
        private bool _traceRequestComparisons = false;

        /// <summary>
        /// Gets the static <see cref="System.Random"/> instance.
        /// </summary>
        internal static Random Random { get; } = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientRequest"/> class.
        /// </summary>
        /// <param name="client">The <see cref="MockHttpClient"/>.</param>
        /// <param name="method">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        internal MockHttpClientRequest(MockHttpClient client, HttpMethod method, string? requestUri)
        {
            _client = client;
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _requestUri = requestUri;

            Rule = new MockHttpClientRequestRule();
            Rule.Response = new MockHttpClientResponse(this, Rule);
            Implementor = _client.Factory.Implementor;
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the singular <see cref="MockHttpClientRequestRule"/>.
        /// </summary>
        internal MockHttpClientRequestRule Rule { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        internal IJsonSerializer JsonSerializer => _client.Factory.JsonSerializer;

        /// <summary>
        /// Gets the <see cref="JsonElementComparerOptions"/>.
        /// </summary>
        internal JsonElementComparerOptions JsonComparerOptions => _client.Factory.JsonComparerOptions;

        /// <summary>
        /// Indicates that the <see cref="MockHttpClientRequest"/> mock has been completed; in that a corresponding response has been provided.
        /// </summary>
        /// <remarks>It is possible to create a <see cref="MockHttpClientRequest"/> without specifying a response which is considered a non-complete state; a corresponding <see cref="MockHttpClientException"/> will be thrown by 
        /// <see cref="Verify"/> if in this state.</remarks>
        public bool IsMockComplete { get; private set; }

        /// <summary>
        /// Mocks the response for rule.
        /// </summary>
        internal void MockResponse()
        {
            // Either Setup or SetupSequence based on rule configuration.
            if (Rule.Responses == null)
            {
                var m = _client.MessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.Is<HttpRequestMessage>(x => RequestPredicate(x)),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns((HttpRequestMessage req, CancellationToken ct) => CreateResponseAsync(req, Rule.Response!, ct));
            }
            else
            {
                var mseq = _client.MessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.Is<HttpRequestMessage>(x => RequestPredicate(x)),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns((HttpRequestMessage req, CancellationToken ct) =>
                    {
                        if (Rule.ResponsesIndex >= Rule.Responses.Count)
                            throw new MockHttpClientException($"There were {Rule.Responses.Count} responses configured for the Sequence and these responses have been exhausted; i.e. an unexpected additional invocation has occured. Request: {ToString()}");

                        return CreateResponseAsync(req, Rule.Responses[Rule.ResponsesIndex++], ct);
                    });
            }

            // Mark as mock complete.
            IsMockComplete = true;
        }

        /// <summary>
        /// Create the <see cref="HttpResponseMessage"/> from the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        private static async Task<HttpResponseMessage> CreateResponseAsync(HttpRequestMessage request, MockHttpClientResponse response, CancellationToken ct)
        {
            response.Count++;

            var httpResponse = new HttpResponseMessage(response.StatusCode) { RequestMessage = request };
            if (response.Content != null)
                httpResponse.Content = response.Content;

            if (!response.HttpHeaders.IsEmpty)
            {
                foreach (var header in response.HttpHeaders)
                {
                    httpResponse.Headers.Add(header.Key, header.Value);
                }
            }

            await response.ExecuteDelayAsync(ct).ConfigureAwait(false);

            response.ResponseAction?.Invoke(httpResponse);
            return httpResponse;
        }

        /// <summary>
        /// Converts the body to a string.
        /// </summary>
        private string BodyToString()
        {
            if (_mediaType == null || _content == null)
                return "no";

            return _mediaType.ToLowerInvariant() switch
            {
                MediaTypeNames.Application.Json => $"'{JsonSerializer.Serialize(_content, JsonWriteFormat.None)}' [{_mediaType}]",
                _ => $"'{_content}' [{_mediaType}]",
            };
        }

        /// <summary>
        /// Check the request and content for a match.
        /// </summary>
        private bool RequestPredicate(HttpRequestMessage request)
        {
            if (request.Method != _method)
                return false;

            var uri = new Uri(_requestUri ?? string.Empty, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri)
            {
                if (_client.IsBaseAddressSpecified && request.RequestUri != uri)
                    return false;

                if (!_client.IsBaseAddressSpecified && !WebUtility.UrlDecode(request.RequestUri!.PathAndQuery).EndsWith(WebUtility.UrlDecode(uri.PathAndQuery)))
                    return false;
            }
            else if (!WebUtility.UrlDecode(request.RequestUri!.PathAndQuery).EndsWith(WebUtility.UrlDecode(uri.OriginalString)))
                return false;

            if (_mediaType == null)
                return request.Content == null;

            if (request?.Content == null)
                return false;

            if (_anyContent)
                return true;

            if (string.Compare(_mediaType, request.Content?.Headers?.ContentType?.MediaType, StringComparison.InvariantCultureIgnoreCase) != 0)
                return false;

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            if (TesterBase.JsonMediaTypeNames.Contains(_mediaType.ToLowerInvariant()))
            {
                try
                {
                    var content = _content is string cstring ? cstring : JsonSerializer.Serialize(_content);
                    var options = JsonComparerOptions.Clone();
                    options.JsonSerializer ??= JsonSerializer;
                    var jcr = new JsonElementComparer(options).Compare(content, body, _pathsToIgnore);
                    if (jcr.HasDifferences && _traceRequestComparisons)
                        Implementor.WriteLine($"UnitTestEx > Mismatched HTTP request {request.Method} {request.RequestUri} mocked vs actual trace comparison differences:{Environment.NewLine}{jcr}");

                    return jcr.AreEqual;
                }
                catch
                {
                    return false;
                }
            }
            else
                 return body == _content?.ToString();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var hc = _client.GetHttpClient();
            return $"<{_client.Name}> {_method} {(hc.BaseAddress == null ? _requestUri : new Uri(hc.BaseAddress!, _requestUri))} {ContentToString()} {(_mediaType == null ? string.Empty : $"({_mediaType})")}";
        }

        /// <summary>
        /// Convert the content to a string.
        /// </summary>
        private string? ContentToString()
        {
            if (_anyContent)
                return "'Any content'";

            if (_content == null)
                return "'No content'";

            if (TesterBase.JsonMediaTypeNames.Contains(_mediaType?.ToLowerInvariant()) && _content is not string)
                return JsonSerializer.Serialize(_content);

            return _content.ToString();
        }

        /// <summary>
        /// Enables <i>any</i> request with <i>a</i> body (functionally equivalent to <see cref="ItExpr.IsAny{TValue}"/>).
        /// </summary>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithAnyBody()
        {
            _anyContent = true;
            _mediaType = MediaTypeNames.Text.Plain;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body as <paramref name="text"/> <see cref="StringContent"/> (<see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="text">The text that represents the <see cref="StringContent"/>.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithBody(string text)
        {
            _content = text;
            _mediaType = MediaTypeNames.Text.Plain; 
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body as <paramref name="body"/> <see cref="StringContent"/> with custom <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="body">The text that represents the <see cref="StringContent"/>.</param>
        /// <param name="mediaType">The media type of the request.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithBody(string body, string mediaType)
        {
            _content = body;
            _mediaType = mediaType;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body with the <paramref name="value"/> to be serialized as JSON (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value that will be converted to JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonBody<T>(T value, params string[] pathsToIgnore)
        {
            _content = value;
            _mediaType = MediaTypeNames.Application.Json;
            _pathsToIgnore = pathsToIgnore;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body with the <paramref name="json"/> content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="json">The <see cref="MediaTypeNames.Application.Json"/> content.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
#if NET7_0_OR_GREATER
        public MockHttpClientRequestBody WithJsonBody([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore)
#else
        public MockHttpClientRequestBody WithJsonBody(string json, params string[] pathsToIgnore)
#endif
        {
            try
            {
                _ = JsonSerializer.Deserialize(json);
                _content = json;
                _mediaType = MediaTypeNames.Application.Json;
                _pathsToIgnore = pathsToIgnore;
                return new MockHttpClientRequestBody(Rule);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"JSON is not considered valid: {ex.Message}");
            }
        }

        /// <summary>
        /// Provides the expected request body using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonResourceBody<TAssembly>(string resourceName, params string[] pathsToIgnore) => WithJsonResourceBody(resourceName, typeof(TAssembly).Assembly, pathsToIgnore);

        /// <summary>
        /// Provides the expected request body using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonResourceBody(string resourceName, Assembly? assembly = null, params string[] pathsToIgnore)
        {
            _content = Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly());
            _pathsToIgnore = pathsToIgnore;
            _mediaType = MediaTypeNames.Application.Json;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Sets the JSON paths to ignore from the comparison.
        /// </summary>
        internal MockHttpClientRequest WithPathsToIgnore(params string[] pathsToIgnore)
        {
            _pathsToIgnore = pathsToIgnore;
            return this;
        }

        /// <summary>
        /// Sets the number of <paramref name="times"/> that the request can be invoked.
        /// </summary>
        /// <param name="times"></param>
        /// <returns>The <see cref="MockHttpClientRequest"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Where not set it will automatically verify that it is called at least once (see <see cref="Moq.Language.IVerifies.Verifiable()"/>).
        /// <para>Each time this is invoked it will override the previously set value.</para></remarks>
        public MockHttpClientRequest Times(Times times)
        {
            Rule.Times = times;
            return this;
        }

        /// <summary>
        /// Indicates whether the request content comparison differences should be trace logged to aid in debugging/troubleshooting.
        /// </summary>
        /// <returns>The <see cref="MockHttpClientRequest"/> to support fluent-style method-chaining.</returns>
        public MockHttpClientRequest TraceRequestComparisons()
        {
            _traceRequestComparisons = true;
            return this;
        }

        /// <summary>
        /// Verifies the request is <see cref="IsMockComplete">complete</see> and was invoked the specified number of <see cref="Times"/> (where specified).
        /// </summary>
        /// <exception cref="MockHttpClientException">Thrown when <see cref="IsMockComplete"/> is <c>false</c>; i.e the configuration is not complete.</exception>
        public void Verify()
        {
            if (!IsMockComplete)
                throw new MockHttpClientException($"The request mock is not completed; the {nameof(MockHttpClientRequest)}.{nameof(IsMockComplete)} must be true for mocking to be verified.");

            if (Rule.Responses == null)
            {
                var times = Rule.Times ?? Moq.Times.AtLeastOnce();
                times.Deconstruct(out var from, out var to);
                if (Rule.Response!.Count < from || Rule.Response!.Count > to)
                    throw new MockHttpClientException($"The request was invoked {Rule.Response!.Count} times; expected {times}. Request: {ToString()}");
            }
            else if (Rule.Responses.Sum(x => x.Count) != Rule.Responses.Count)
                throw new MockHttpClientException($"There were {Rule.Responses.Count} response(s) configured for the Sequence and only {Rule.Responses.Sum(x => x.Count)} response(s) invoked. Request: {ToString()}");
        }

        /// <summary>
        /// Gets the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        public MockHttpClientResponse Respond => Rule.Response!;
    }
}