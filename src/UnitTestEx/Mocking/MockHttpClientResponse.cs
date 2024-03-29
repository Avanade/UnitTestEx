// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Json;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the <see cref="HttpResponseMessage"/> configuration for mocking.
    /// </summary>
    public sealed class MockHttpClientResponse
    {
        private readonly MockHttpClientRequest _clientRequest;
        private readonly MockHttpClientRequestRule? _rule;
        private Func<TimeSpan>? _delay;

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
        /// Executes the <see cref="Delay(Func{TimeSpan})"/>; i.e. goes to sleep for the predetermined time.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        internal Task ExecuteDelayAsync(CancellationToken cancellationToken) => _delay == null ? Task.CompletedTask : Task.Delay(_delay(), cancellationToken);

        /// <summary>
        /// Gets or sets the number of times responded count.
        /// </summary>
        internal int Count { get; set; }

        /// <summary>
        /// Sets the simulated delay (sleep) for the response.
        /// </summary>
        /// <param name="timeSpan">The delay (sleep) function.</param>
        /// <returns>The <see cref="MockHttpClientResponse"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Each time a <c>Delay</c> is invoked it will override the previously set value.</remarks>
        public MockHttpClientResponse Delay(Func<TimeSpan> timeSpan)
        {
            _delay = timeSpan;
            return this;
        }

        /// <summary>
        /// Sets the simulated delay (sleep) for the response.
        /// </summary>
        /// <param name="timeSpan">The delay (sleep) <see cref="TimeSpan"/>.</param>
        /// <returns>The <see cref="MockHttpClientResponse"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Each time a <c>Delay</c> is invoked it will override the previously set value.</remarks>
        public MockHttpClientResponse Delay(TimeSpan timeSpan) => Delay(() => timeSpan);

        /// <summary>
        /// Sets the simulated delay (sleep) as a random between the <paramref name="from"/> and <paramref name="to"/> values for the response.
        /// </summary>
        /// <param name="from">The from <see cref="TimeSpan"/>.</param>
        /// <param name="to">The to <see cref="TimeSpan"/>.</param>
        /// <returns>The <see cref="MockHttpClientResponse"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Each time a <c>Delay</c> is invoked it will override the previously set value.</remarks>
        public MockHttpClientResponse Delay(TimeSpan from, TimeSpan to)
        {
            if (to < from)
                throw new ArgumentException("From must be less than or equal to the To value.", nameof(from));

            return Delay(() => TimeSpan.FromMilliseconds(MockHttpClientRequest.Random.Next((int)from.TotalMilliseconds, (int)to.TotalMilliseconds)));
        }

        /// <summary>
        /// Sets the simulated delay (sleep) for the response.
        /// </summary>
        /// <param name="millseconds">The delay (sleep) milliseconds.</param>
        /// <returns>The <see cref="MockHttpClientResponse"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Each time a <c>Delay</c> is invoked it will override the previously set value.</remarks>
        public MockHttpClientResponse Delay(int millseconds) => Delay(TimeSpan.FromMilliseconds(millseconds));

        /// <summary>
        /// Sets the simulated delay (sleep) as a random between the <paramref name="from"/> and <paramref name="to"/> values for the response.
        /// </summary>
        /// <param name="from">The from milliseconds.</param>
        /// <param name="to">The to millseconds.</param>
        /// <returns>The <see cref="MockHttpClientResponse"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Each time a <c>Delay</c> is invoked it will override the previously set value.</remarks>
        public MockHttpClientResponse Delay(int from, int to) => Delay(TimeSpan.FromMilliseconds(from), TimeSpan.FromMilliseconds(to));

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
        public void WithJson<T>(T value, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null) => WithJson(_clientRequest.JsonSerializer.Serialize(value, JsonWriteFormat.None), statusCode, response);

        /// <summary>
        /// Provides the mocked response using the <paramref name="json"/> formatted string as the content.
        /// </summary>
        /// <param name="json">The <see cref="MediaTypeNames.Application.Json"/> content.</param>
        /// <param name="statusCode">The optional <see cref="HttpStatusCode"/> (defaults to <see cref="HttpStatusCode.OK"/>).</param>
        /// <param name="response">The optional action to enable additional configuration of the <see cref="HttpResponseMessage"/>.</param>
#if NET7_0_OR_GREATER
        public void WithJson([StringSyntax(StringSyntaxAttribute.Json)] string json, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null)
#else
        public void WithJson(string json, HttpStatusCode? statusCode = null, Action<HttpResponseMessage>? response = null)
#endif
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

            ArgumentNullException.ThrowIfNull(sequence);

            _rule.Responses ??= [];

            var s = new MockHttpClientResponseSequence(_clientRequest, _rule);
            sequence(s);

            if (_rule.Responses.Count > 0)
                _clientRequest.MockResponse();
        }
    }
}