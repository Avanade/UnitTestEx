// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using KellermanSoftware.CompareNetObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="HttpRequestMessage"/> configuration for mocking.
    /// </summary>
    public class MockHttpClientRequest
    {
        private readonly MockHttpClient _client;
        private readonly HttpMethod _method;
        private readonly string _requestUri;
        private bool _anyContent;
        private object? _content;
        private string? _mediaType;
        private string[] _membersToIgnore = Array.Empty<string>();
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
        internal MockHttpClientRequest(MockHttpClient client, HttpMethod method, string requestUri)
        {
            _client = client;
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));

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
                    .Returns((HttpRequestMessage x, CancellationToken ct) => CreateResponseAsync(Rule.Response!, ct));
            }
            else
            {
                var mseq = _client.MessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.Is<HttpRequestMessage>(x => RequestPredicate(x)),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns((HttpRequestMessage x, CancellationToken ct) =>
                    {
                        if (Rule.ResponsesIndex >= Rule.Responses.Count)
                            throw new MockHttpClientException($"There were {Rule.Responses.Count} responses configured for the Sequence and these responses have been exhausted; i.e. an unexpected additional invocation has occured. Request: {ToString()}");

                        return CreateResponseAsync(Rule.Responses[Rule.ResponsesIndex++], ct);
                    });
            }

            // Mark as mock complete.
            IsMockComplete = true;
        }

        /// <summary>
        /// Create the <see cref="HttpResponseMessage"/> from the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        private static async Task<HttpResponseMessage> CreateResponseAsync(MockHttpClientResponse response, CancellationToken ct)
        {
            response.Count++;

            var httpResponse = new HttpResponseMessage(response.StatusCode);
            if (response.Content != null)
                httpResponse.Content = response.Content;

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
            if (request.Method != _method || !request.RequestUri!.OriginalString.EndsWith(_requestUri, StringComparison.InvariantCultureIgnoreCase))
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

            switch (_mediaType.ToLowerInvariant())
            {
                // Deserialize the JSON and compare.
                case MediaTypeNames.Application.Json:
                    try
                    {
                        if (_content is string cstr)
                        {
                            var cur = new Utf8JsonReader(new BinaryData(cstr));
                            var bur = new Utf8JsonReader(new BinaryData(body));

                            if (JsonElement.TryParseValue(ref cur, out JsonElement? cje) && JsonElement.TryParseValue(ref bur, out JsonElement? bje))
                            {
                                var differences = new JsonElementComparer(5).Compare((JsonElement)cje, (JsonElement)bje, _membersToIgnore);
                                if (differences != null && _traceRequestComparisons)
                                    Implementor.CreateLogger("MockHttpClientRequest").LogTrace($"HTTP request JsonElementComparer differences: {differences}");

                                return differences == null;
                            }
                        }

                        var cc = ObjectComparer.CreateDefaultConfig();
                        cc.MembersToIgnore.AddRange(_membersToIgnore!);

                        var cl = new CompareLogic(cc);
                        var cv = JsonSerializer.Deserialize(body, _content!.GetType());
                        var cr = cl.Compare(_content, cv);
                        if (!cr.AreEqual && _traceRequestComparisons)
                            Implementor.CreateLogger("MockHttpClientRequest").LogTrace($"HTTP request ObjectComparer differences: {cr.DifferencesString}");

                        return cr.AreEqual;
                    }
                    catch
                    {
                        return false;
                    }

                // For any other content type, just compare the body.
                case MediaTypeNames.Text.Plain:
                case MediaTypeNames.Text.Xml:
                case MediaTypeNames.Text.Html:
                case MediaTypeNames.Application.Soap:
                case MediaTypeNames.Application.Xml:
                default:
                    return body == _content?.ToString();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"<{_client.Name}> {_method} {(_client.HttpClient.BaseAddress == null ? _requestUri : new Uri(_client.HttpClient.BaseAddress, _requestUri))} {ContentToString()} {(_mediaType == null ? string.Empty : $"({_mediaType})")}";

        /// <summary>
        /// Convert the content to a string.
        /// </summary>
        private string? ContentToString()
        {
            if (_anyContent)
                return "'Any content'";

            if (_content == null)
                return "'No content'";

            if (_mediaType?.ToLowerInvariant() == MediaTypeNames.Application.Json && _content is not string)
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
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonBody<T>(T value, params string[] membersToIgnore)
        {
            _content = value;
            _mediaType = MediaTypeNames.Application.Json;
            _membersToIgnore = membersToIgnore;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body with the <paramref name="json"/> content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="json">The <see cref="MediaTypeNames.Application.Json"/> content.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonBody(string json, params string[] pathsToIgnore)
        {
            try
            {
                _ = JsonSerializer.Deserialize(json);
                _content = json;
                _mediaType = MediaTypeNames.Application.Json;
                _membersToIgnore = pathsToIgnore;
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
            _membersToIgnore = pathsToIgnore;
            _mediaType = MediaTypeNames.Application.Json;
            return new MockHttpClientRequestBody(Rule);
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