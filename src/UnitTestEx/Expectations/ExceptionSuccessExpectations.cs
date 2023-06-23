// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Abstractions;
using CoreEx.Entities;
using System;
using System.Linq;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides execution exception or success expectations.
    /// </summary>
    public class ExceptionSuccessExpectations
    {
        private bool _expectException;
        private Type? _expectExceptionType;
        private string? _expectExceptionMessage;
        private MessageItemCollection? _expectMessages;
        private string? _expectErrorType;
        private bool _expectSuccess;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionSuccessExpectations"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public ExceptionSuccessExpectations(TestFrameworkImplementor implementor) => Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Expects that an <see cref="Exception"/> will be thrown during execution.
        /// </summary>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void SetExpectException(string? expectedMessage = null) => VerifyConsistency(true, () =>
        {
            _expectException = true;
            _expectExceptionMessage = expectedMessage;
        });

        /// <summary>
        /// Expects that the <typeparamref name="TException"/> will be thrown during execution.
        /// </summary>
        /// <typeparam name="TException">The <see cref="Exception"/> <see cref="Type"/>.</typeparam>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void SetExpectException<TException>(string? expectedMessage = null) where TException : Exception => VerifyConsistency(true, () => SetExpectException(typeof(TException), expectedMessage));

        /// <summary>
        /// Expects that an <see cref="Exception"/> of <see cref="Type"/> <paramref name="exceptionType"/> will be thrown during execution.
        /// </summary>
        /// <param name="exceptionType">The <see cref="Exception"/> <see cref="Type"/>.</param>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void SetExpectException(Type exceptionType, string? expectedMessage = null) => VerifyConsistency(true, () =>
        {
            _expectException = true;
            _expectExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
            _expectExceptionMessage = expectedMessage;
        });

        /// <summary>
        /// Expects that an <see cref="IExtendedException"/> <see cref="Exception"/> with the specified <paramref name="errorType"/> will be thrown during execution.
        /// </summary>
        /// <param name="errorType">The error type.</param>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void SetExpectErrorType(string errorType, string? expectedMessage = null) => VerifyConsistency(true, () =>
        {
            _expectException = true;
            _expectErrorType = errorType ?? throw new ArgumentNullException(null);
            _expectExceptionMessage = expectedMessage;
        });

        /// <summary>
        /// Expect a <see cref="ValidationException"/> was thrown during execution with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        public void SetExpectErrors(params string[] messages) => VerifyConsistency(true, () =>
        {
            var mic = new MessageItemCollection();
            messages.ForEach(m => mic.AddError(m));
            SetExpectErrors(mic);
        });

        /// <summary>
        /// Expect a <see cref="ValidationException"/> was thrown during execution with the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <remarks>Will only check the <see cref="ApiError.Field"/> where specified (not <c>null</c>).</remarks>
        public void SetExpectErrors(params ApiError[] errors) => VerifyConsistency(true, () =>
        {
            var mic = new MessageItemCollection();
            errors.ForEach(e => mic.Add(MessageItem.CreateErrorMessage(e.Field, e.Message)));
            SetExpectErrors(mic);
        });

        /// <summary>
        /// Expect a <see cref="ValidationException"/> was thrown during execution with the specified <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public void SetExpectErrors(MessageItemCollection messages) => VerifyConsistency(true, () =>
        {
            _expectException = true;
            _expectExceptionType = typeof(ValidationException);
            _expectMessages = messages;
        });

        /// <summary>
        /// Expects that the execution was successful; i.e. there was no <see cref="Exception"/> thrown.
        /// </summary>
        public void SetExpectSuccess() => VerifyConsistency(false, () => _expectSuccess = true);

        /// <summary>
        /// Verify expectation consistency.
        /// </summary>
        private void VerifyConsistency(bool expectingError, Action action)
        {
            if (expectingError && _expectSuccess)
                throw new InvalidOperationException("Can not add an exception/error expectation where a success expectation has already been defined.");

            if (!expectingError && _expectException)
                throw new InvalidOperationException("Can not add a success expectation/error where an exception expectation has already been defined.");

            action();
        }

        /// <summary>
        /// Asserts the expectations.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> if any.</param>
        public void Assert(Exception? exception)
        {
            if (_expectException)
            {
                if (exception == null)
                    Implementor.AssertFail("Expected an exception; however, the execution was successful.");

                if (_expectExceptionType != null && _expectExceptionType != exception!.GetType())
                    Implementor.AssertFail($"Expected Exception type '{_expectExceptionType.Name}' not equal to actual '{exception!.GetType().Name}'.");

                if (_expectErrorType != null)
                {
                    if (exception is IExtendedException eex)
                        Implementor.AssertAreEqual(_expectErrorType, eex.ErrorType, $"Expected ErrorType '{_expectErrorType}' not equal to actual '{eex.ErrorType}'.");
                    else
                        Implementor.AssertFail($"Expected an ErrorType of {_expectErrorType}; however, the exception '{exception!.GetType().Name}' does not implement {nameof(IExtendedException)}.");
                }

                if (_expectExceptionMessage != null)
                    Implementor.AssertAreEqual(_expectExceptionMessage, exception!.Message, $"Expected Exception message '{_expectExceptionMessage}' not equal to actual '{exception!.Message}'.");

                if (exception is ValidationException vex && _expectMessages != null)
                {
                    if (!ExpectationsExtensions.TryAreMessagesMatched(_expectMessages, vex.Messages, out var errorMessage))
                        Implementor.AssertFail(errorMessage);
                }
            }

            if (_expectSuccess)
            {
                if (exception != null)
                    Implementor.AssertFail($"Expected success; however, a '{exception.GetType().Name}' was thrown: {exception.Message}");
            }
        }
    }
}