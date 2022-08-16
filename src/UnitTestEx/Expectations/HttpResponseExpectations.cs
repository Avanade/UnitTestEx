// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Abstractions;
using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Json;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the ability to set up-front <see cref="HttpResponseMessage"/> expectations versus post execution asserts.
    /// </summary>
    public class HttpResponseExpectations
    {
        private HttpStatusCode? _expectedStatusCode;

        /// <summary>
        /// Gets the deserialized value from the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        /// <returns>The deserialized value.</returns>
        public static TValue? GetValueFromHttpResponseMessage<TValue>(HttpResponseMessage response, IJsonSerializer jsonSerializer)
        {
            try
            {
                var json = (response ?? throw new ArgumentNullException(nameof(response))).Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var val = (jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer))).Deserialize<TValue>(json);
                if (val is ICollectionResult cr && response.TryGetPagingResult(out var paging))
                    cr.Paging = paging;

                return val;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                return default;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseExpectations"/> class.
        /// </summary>
        /// <param name="tester">The parent/owning <see cref="TesterBase"/>.</param>
        public HttpResponseExpectations(TesterBase tester) => Tester = tester;

        /// <summary>
        /// Gets the <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Tester { get; }

        /// <summary>
        /// Expect a response with the specified <see cref="HttpStatusCode"/>.
        /// </summary>
        /// <param name="statusCode">The expected <see cref="HttpStatusCode"/>.</param>
        public void SetExpectStatusCode(HttpStatusCode statusCode) => _expectedStatusCode = statusCode;

        /// <summary>
        /// Performs an assert of the expectations.
        /// </summary>
        /// <param name="result">The <see cref="HttpResult"/>.</param>
        public virtual void Assert(HttpResult result)
        {
            if (_expectedStatusCode.HasValue && _expectedStatusCode != result.StatusCode)
                Tester.Implementor.AssertFail($"Expected StatusCode value of '{_expectedStatusCode.Value} ({(int)_expectedStatusCode.Value})'; actual was '{result.StatusCode} ({(int)result.StatusCode})'.");
        }
    }
}