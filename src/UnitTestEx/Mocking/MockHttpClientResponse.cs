// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="HttpResponseMessage"/> configuration for mocking.
    /// </summary>
    public class MockHttpClientResponse
    {
        private readonly MockHttpClientRequest _clientRequest;
        private readonly MockHttpClientRequestRule? _rule;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientResponse"/> class.
        /// </summary>
        /// <param name="clientRequest">The <see cref="MockHttpClientRequest"/>.</param>
        /// <param name="rule">The <see cref="MockHttpClientRequestRule"/>.</param>
        internal MockHttpClientResponse(MockHttpClientRequest clientRequest, MockHttpClientRequestRule? rule)
        {
            _clientRequest = clientRequest;
            _rule = rule;
        }

        /// <summary>
        /// Gets or sets the <see cref="HttpContent"/>.
        /// </summary>
        internal HttpContent? Content { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="HttpStatusCode"/>.
        /// </summary>
        internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// Gets or sets the optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.
        /// </summary>
        internal Action<HttpResponseMessage>? ResponseAction { get; set; }

        /// <summary>
        /// Provides the mocked response.
        /// </summary>
        /// <param name="content">The optional <see cref="HttpContent"/>.</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void With(HttpContent? content = null, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null)
        {
            Content = content;
            StatusCode = statusCode ?? HttpStatusCode.OK;
            ResponseAction = response;

            if (_rule != null)
                _clientRequest.MockResponse();
        }

        /// <summary>
        /// Provides the mocked response for the specified <paramref name="statusCode"/> (no content).
        /// </summary>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void With(HttpStatusCode statusCode, Action<HttpResponseMessage>? response = null) => With((HttpContent?)null, statusCode, response);

        /// <summary>
        /// Provides the mocked response using the <see cref="string"/> content.
        /// </summary>
        /// <param name="content">The <see cref="string"/> content.</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void With(string content, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null) => With(new StringContent(content ?? throw new ArgumentNullException(nameof(content))), statusCode, response);

        /// <summary>
        /// Provides the mocked response using the <paramref name="value"/> which will be automatically converted to JSON content.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to convert to <see cref="MediaTypeNames.Application.Json"/> content.</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void WithJson<T>(T value, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null) => WithJson(JsonConvert.SerializeObject(value), statusCode, response);

        /// <summary>
        /// Provides the mocked response using the <paramref name="json"/> formatted string as the content.
        /// </summary>
        /// <param name="json">The <see cref="MediaTypeNames.Application.Json"/> content.</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void WithJson(string json, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null)
        {
            var content = new StringContent(json ?? throw new ArgumentNullException(nameof(json)));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json);
            With(content, statusCode, response);
        }

        /// <summary>
        /// Provides the mocked response using the JSON formatted embedded resource string as the content.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        public void WithJsonResource<TAssembly>(string resourceName, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null)
            => WithJsonResource(resourceName, statusCode, response, typeof(TAssembly).Assembly);

        /// <summary>
        /// Provides the mocked response using the JSON formatted embedded resource string as the content.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        public void WithJsonResource(string resourceName, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null, Assembly? assembly = null)
        {
            using var sr = Resource.GetStream(resourceName, assembly ?? Assembly.GetCallingAssembly());
            WithJson(sr.ReadToEnd(), statusCode, response);
        }

        /// <summary>
        /// Provides the means to mock a sequence of one or more responses for the <see cref="MockHttpClientRequest"/>.
        /// </summary>
        /// <param name="sequence">The action to enable the addition of one or more responses (see <see cref="MockHttpClientResponseSequence.Respond"/>).</param>
        public void WithSequence(Action<MockHttpClientResponseSequence> sequence)
        {
            if (_rule == null)
                throw new InvalidOperationException("A WithSequence can not be issued within the context of a parent WithSequence.");

            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            if (_rule.Responses == null)
                _rule.Responses = new List<MockHttpClientResponse>();

            var s = new MockHttpClientResponseSequence(_clientRequest, _rule);
            sequence(s);

            if (_rule.Responses.Count > 0)
                _clientRequest.MockResponse();
        }
    }
}