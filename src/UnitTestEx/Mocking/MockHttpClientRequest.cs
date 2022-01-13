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
        /// Initializes a new instance of the <see cref="MockHttpClientRequest"/> class with body configuration.
        /// </summary>
        /// <param name="client">The <see cref="MockHttpClient"/>.</param>
        /// <param name="method">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="content">The body content/value.</param>
        /// <param name="mediaType">The media type.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        internal MockHttpClientRequest(MockHttpClient client, HttpMethod method, string requestUri, object? content, string mediaType, string[] membersToIgnore) : this(client, method, requestUri)
        {
            _content = content;
            _mediaType = mediaType;
            _membersToIgnore = membersToIgnore;
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
        /// Mocks the response for rule.
        /// </summary>
        /// <param name="rule">The <see cref="MockHttpClientRequestRule"/>.</param>
        internal void MockResponse(MockHttpClientRequestRule rule)
        {
            // Do not perform mock logic below unless it is for the single rule only. 
            if (rule != Rule)
                return;

            _client.MessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(x => x.Method == _method && x.RequestUri.ToString().EndsWith(_requestUri, StringComparison.InvariantCultureIgnoreCase) && RequestContentPredicate(x)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var resp = new HttpResponseMessage(Rule.Response!.StatusCode);
                    if (Rule.Response.Content != null)
                        resp.Content = Rule.Response.Content;

                    Rule.Response.ResponseAction?.Invoke(resp);
                    return resp;
                })
                .Verifiable($"{_method} '{_requestUri}' request with {BodyToString()} body");
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
                case MediaTypeNames.Text.Plain:
                    return $"'{_content}' [{_mediaType}]";

                case MediaTypeNames.Application.Json:
                    if (_content is JToken jt)
                        return $"'{jt.ToString(Formatting.None)}' [{_mediaType}]";
                    else
                        return $"'{JsonConvert.SerializeObject(_content, Formatting.None)}' [{_mediaType}]";

                default:
                    return "???";
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
                case MediaTypeNames.Text.Plain:
                    return body == _content?.ToString();

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

                default:
                    return false;
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
        /// Gets the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        public MockHttpClientResponse Respond => Rule.Response!;
    }
}