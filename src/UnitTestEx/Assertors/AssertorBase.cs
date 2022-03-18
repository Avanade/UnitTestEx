// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the base test assert helper that distinguishes between <see cref="AssertException"/> and <see cref="AssertSuccess"/>.
    /// </summary>
    public abstract class AssertorBase<TSelf> where TSelf : AssertorBase<TSelf>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssertorBase{TSelf}"/> class.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> (if any).</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal AssertorBase(Exception? exception, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer)
        {
            Exception = exception;
            Implementor = implementor;
            JsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Gets the <see cref="System.Exception"/>.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        protected IJsonSerializer JsonSerializer { get; }

        /// <summary>
        /// Asserts that an <see cref="Exception"/> was thrown during execution.
        /// </summary>
        /// <returns></returns>
        public TSelf AssertException()
        {
            if (Exception == null)
                Implementor.AssertFail("Expected an exception; however, the execution was successful.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that an <see cref="Exception"/> was thrown during execution.
        /// </summary>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public TSelf AssertException<TException>(string? expectedMessage = null) where TException : Exception
        {
            AssertException();
            Implementor.AssertIsType<TException>(Exception!, $"Expected Exception type '{typeof(TException).Name}' not equal to actual '{Exception!.GetType().Name}'.");
            if (expectedMessage != null && expectedMessage != Exception.Message)
                Implementor.AssertAreEqual(expectedMessage, Exception.Message, $"Expected Exception message '{expectedMessage}' not equal to actual '{Exception.Message}'.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the run/exception was successful; i.e. there was no <see cref="Exception"/> thrown.
        /// </summary>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public TSelf AssertSuccess()
        {
            if (Exception != null)
                Implementor.AssertFail($"Expected success; however, a '{Exception.GetType().Name}' was thrown: {Exception.Message}");

            return (TSelf)this;
        }
    }
}