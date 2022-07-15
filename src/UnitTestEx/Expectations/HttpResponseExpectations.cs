// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Abstractions;
using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Json;
using System;
using System.Linq;
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
        private int? _expectedErrorCode;
        private string? _expectedErrorMessage;
        private MessageItemCollection? _expectedMessages;
        private EventExpectations? _expectedEvents;

        /// <summary>
        /// Gets the deserialized value from the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        /// <returns>The deserialized value.</returns>
        public static TValue? GetValueFromHttpResponseMessage<TValue>(HttpResponseMessage response, IJsonSerializer jsonSerializer)
        {
            var json = (response ?? throw new ArgumentNullException(nameof(response))).Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var val = (jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer))).Deserialize<TValue>(json);
            if (val is ICollectionResult cr && response.TryGetPagingResult(out var paging))
                cr.Paging = paging;

            return val;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseExpectations"/> class.
        /// </summary>
        /// <param name="tester">The parent/owning <see cref="TesterBase"/>.</param>
        internal HttpResponseExpectations(TesterBase tester) => Tester = tester;

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
        /// Expect a response with the specified <see cref="ErrorType"/>.
        /// </summary>
        /// <param name="errorCode">The expected error code.</param>
        /// <param name="errorMessage">The expected error message text; where not specified the error message text will not be checked.</param>
        public void SetExpectErrorType(int errorCode, string? errorMessage = null)
        {
            _expectedErrorCode = errorCode;
            _expectedErrorMessage = errorMessage;
        }

        /// <summary>
        /// Expect a response with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        public void SetExpectMessages(params string[] messages)
        {
            var mic = new MessageItemCollection();
            messages.ForEach(m => mic.AddError(m));
            SetExpectMessages(mic);
        }

        /// <summary>
        /// Expect a response with the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <remarks>Will only check the <see cref="ApiError.Field"/> where specified (not <c>null</c>).</remarks>
        public void SetExpectMessages(params ApiError[] errors)
        {
            var mic = new MessageItemCollection();
            errors.ForEach(e => mic.Add(MessageItem.CreateErrorMessage(e.Field, e.Message)));
            SetExpectMessages(mic);
        }

        /// <summary>
        /// Expect a response with the specified <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <remarks>Will only check the <see cref="MessageItem.Property"/> where specified (not <c>null</c>).</remarks>
        public void SetExpectMessages(MessageItemCollection messages)
        {
            if (messages == null) 
                throw new ArgumentNullException(nameof(messages));

            if (_expectedMessages == null)
                _expectedMessages = new MessageItemCollection();

            _expectedMessages.AddRange(messages);
        }

        /// <summary>
        /// Gets the <see cref="ExpectedEvents"/>.
        /// </summary>
        public EventExpectations ExpectedEvents => _expectedEvents ??= new(Tester);

        /// <summary>
        /// Performs an assert of the expectations.
        /// </summary>
        /// <param name="result">The <see cref="HttpResult"/>.</param>
        public virtual void Assert(HttpResult result)
        {
            if (_expectedStatusCode.HasValue && _expectedStatusCode != result.StatusCode)
                Tester.Implementor.AssertAreEqual(_expectedStatusCode.Value, result.StatusCode, "ExpectStatusCode");

            if (_expectedErrorCode.HasValue && _expectedErrorCode != result.ErrorCode)
                Tester.Implementor.AssertAreEqual(_expectedErrorCode, result.ErrorCode, "ExpectErrorType or ExpectErrorCode");

            if (_expectedErrorMessage != null && _expectedErrorMessage != result.Content)
                Tester.Implementor.AssertAreEqual(_expectedErrorMessage, result.Content, "ExpectErrorType or ExpectErrorCode message");

            if (_expectedMessages != null && !ExpectationsExtensions.TryAreMessagesMatched(_expectedMessages, result.Messages, out var errorMessage))
                Tester.Implementor.AssertFail(errorMessage);

            if (_expectedEvents != null)
                _expectedEvents.Assert();
        }
    }
}