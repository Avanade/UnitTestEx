// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
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
        private bool _expectSuccess;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionSuccessExpectations"/> class.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        public ExceptionSuccessExpectations(TesterBase tester) => Tester = tester ?? throw new ArgumentNullException(nameof(tester));

        /// <summary>
        /// Gets the <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Tester { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor => Tester.Implementor;

        /// <summary>
        /// Expects that an <see cref="Exception"/> will be thrown during execution.
        /// </summary>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void ExpectException(string? expectedMessage = null)
        {
            _expectException = true;
            _expectExceptionMessage = expectedMessage;
        }

        /// <summary>
        /// Expects that the <typeparamref name="TException"/> will be thrown during execution.
        /// </summary>
        /// <typeparam name="TException">The <see cref="Exception"/> <see cref="Type"/>.</typeparam>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void ExpectException<TException>(string? expectedMessage = null) where TException : Exception => ExpectException(typeof(TException), expectedMessage);

        /// <summary>
        /// Expects that an <see cref="Exception"/> of <see cref="Type"/> <paramref name="exceptionType"/> will be thrown during execution.
        /// </summary>
        /// <param name="exceptionType">The <see cref="Exception"/> <see cref="Type"/>.</param>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        public void ExpectException(Type exceptionType, string? expectedMessage = null)
        {
            _expectException = true;
            _expectExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
            _expectExceptionMessage = expectedMessage;
        }

        /// <summary>
        /// Expects that the execution was successful; i.e. there was no <see cref="Exception"/> thrown.
        /// </summary>
        public void ExpectSuccess() => _expectSuccess = true;

        /// <summary>
        /// Asserts the expectations.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> is any.</param>
        public void Assert(Exception? exception)
        {
            if (_expectException)
            {
                if (exception == null)
                    Implementor.AssertFail("Expected an exception; however, the execution was successful.");

                if (_expectExceptionType != null)
                    Implementor.AssertIsType(_expectExceptionType, exception!, $"Expected Exception type '{_expectExceptionType.GetType().Name}' not equal to actual '{exception!.GetType().Name}'.");

                if (_expectExceptionMessage != null && _expectExceptionMessage != exception!.Message)
                    Implementor.AssertAreEqual(_expectExceptionMessage, exception!.Message, $"Expected Exception message '{_expectExceptionMessage}' not equal to actual '{exception!.Message}'.");
            }

            if (_expectSuccess)
            {
                if (exception != null)
                    Implementor.AssertFail($"Expected success; however, a '{exception.GetType().Name}' was thrown: {exception.Message}");
            }
        }
    }
}