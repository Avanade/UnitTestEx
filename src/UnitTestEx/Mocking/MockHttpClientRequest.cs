// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using KellermanSoftware.CompareNetObjects;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
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
                        ItExpr.Is<HttpRequestMessage>(x => x.Method == _method && x.RequestUri.ToString().EndsWith(_requestUri, StringComparison.InvariantCultureIgnoreCase) && RequestContentPredicate(x)),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(() =>
                    {
                        var response = Rule.Response!;
                        response.ExecuteDelay();
                        var httpResponse = new HttpResponseMessage(response.StatusCode);
                        if (response.Content != null)
                            httpResponse.Content = response.Content;

                        response.ResponseAction?.Invoke(httpResponse);
                        return httpResponse;
                    });

                if (Rule.Times == null)
                    m.Verifiable($"{_method} '{_requestUri}' request with {BodyToString()} body");
            }
            else
            {
                var mseq = _client.MessageHandler.Protected()
                    .SetupSequence<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.Is<HttpRequestMessage>(x => x.Method == _method && x.RequestUri.ToString().EndsWith(_requestUri, StringComparison.InvariantCultureIgnoreCase) && RequestContentPredicate(x)),
                        ItExpr.IsAny<CancellationToken>());

                foreach (var response in Rule.Responses)
                {
                    mseq.ReturnsAsync(() =>
                    {
                        var httpResponse = new HttpResponseMessage(response.StatusCode);
                        response.ExecuteDelay();
                        if (response.Content != null)
                            httpResponse.Content = response.Content;

                        response.ResponseAction?.Invoke(httpResponse);
                        return httpResponse;
                    });
                }
            }

            // Mark as mock complete.
            IsMockComplete = true;
        }

        /// <summary>
        /// Converts the body to a string.
        /// </summary>
        private string BodyToString()
        {
            if (_mediaType == null || _content == null)
                return "no";

            switch (_mediaType.ToLowerInvariant())
            {
                case MediaTypeNames.Application.Json:
                    if (_content is JToken jt)
                        return $"'{jt.ToString(Formatting.None)}' [{_mediaType}]";
                    else
                        return $"'{JsonConvert.SerializeObject(_content, Formatting.None)}' [{_mediaType}]";

                default:
                    return $"'{_content}' [{_mediaType}]";
            }
        }

        /// <summary>
        /// Check the request content for a match.
        /// </summary>
        private bool RequestContentPredicate(HttpRequestMessage request)
        {
            if (_mediaType == null)
                return request.Content == null;

            if (request.Content == null)
                return false;

            if (_anyContent)
                return true;

            if (string.Compare(_mediaType, request.Content.Headers.ContentType.MediaType, StringComparison.InvariantCultureIgnoreCase) != 0)
                return false;

            var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            switch (_mediaType.ToLowerInvariant())
            {

                case MediaTypeNames.Application.Json:
                    try
                    {
                        if (_content is JToken jte)
                        {
                            var jta = JToken.Parse(body);
                            return JToken.DeepEquals(jte, jta);
                        }

                        var cc = ObjectComparer.CreateDefaultConfig();
                        cc.MembersToIgnore.AddRange(_membersToIgnore!);

                        var cl = new CompareLogic(cc);
                        var cv = JsonConvert.DeserializeObject(body, _content!.GetType());
                        var cr = cl.Compare(_content, cv);
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
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonBody(string json)
        {
            _content = JToken.Parse(json);
            _mediaType = MediaTypeNames.Application.Json;
            return new MockHttpClientRequestBody(Rule);
        }

        /// <summary>
        /// Provides the expected request body using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonResourceBody<TAssembly>(string resourceName) => WithJsonResourceBody(resourceName, typeof(TAssembly).Assembly);

        /// <summary>
        /// Provides the expected request body using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The resulting <see cref="MockHttpClientRequestBody"/> to <see cref="MockHttpClientRequestBody.Respond"/> accordingly.</returns>
        public MockHttpClientRequestBody WithJsonResourceBody(string resourceName, Assembly? assembly = null)
        {
            _content = Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly());
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
        /// Verifies the request is <see cref="IsMockComplete">complete</see> and was invoked the specified number of <see cref="Times"/> (where specified).
        /// </summary>
        /// <exception cref="MockHttpClientException">Thrown when <see cref="IsMockComplete"/> is <c>false</c>; i.e the configuration is not complete.</exception>
        public void Verify()
        {
            if (!IsMockComplete)
                throw new MockHttpClientException($"The request mock is not completed; the {nameof(MockHttpClientRequest)}.{nameof(IsMockComplete)} must be true for mocking to be verified.");

            if (Rule?.Times != null)
            {
                _client.MessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>("SendAsync", Rule.Times.Value,
                        ItExpr.Is<HttpRequestMessage>(x => x.Method == _method && x.RequestUri.ToString().EndsWith(_requestUri, StringComparison.InvariantCultureIgnoreCase) && RequestContentPredicate(x)),
                        ItExpr.IsAny<CancellationToken>());
            }
        }

        /// <summary>
        /// Gets the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        public MockHttpClientResponse Respond => Rule.Response!;
    }
}