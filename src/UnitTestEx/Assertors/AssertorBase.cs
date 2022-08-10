// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Entities;
using CoreEx.Json;
using System;
using System.Linq;
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
        /// <typeparam name="TException">The <see cref="Exception"/> <see cref="Type"/>.</typeparam>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public TSelf AssertException<TException>(string? expectedMessage = null) where TException : Exception
        {
            AssertException();
            if (Exception!.GetType() != typeof(TException))
                Implementor.AssertFail($"Expected Exception type '{typeof(TException).Name}' not equal to actual '{Exception!.GetType().Name}'.");

            if (expectedMessage != null && expectedMessage != Exception.Message)
                Implementor.AssertAreEqual(expectedMessage, Exception.Message, $"Expected Exception message '{expectedMessage}' not equal to actual '{Exception.Message}'.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that a <see cref="ValidationException"/> was thrown during execution with the specified <see cref="MessageType.Error"/> messages.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageType.Error"/> message texts.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public virtual TSelf AssertErrors(params string[] messages)
        {
            var mic = new MessageItemCollection();
            messages.ForEach(m => mic.AddError(m));
            return AssertErrors(mic);
        }

        /// <summary>
        /// Asserts that a <see cref="ValidationException"/> was thrown during execution with the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public virtual TSelf AssertErrors(params ApiError[] errors)
        {
            var mic = new MessageItemCollection();
            errors.ForEach(e => mic.Add(MessageItem.CreateErrorMessage(e.Field, e.Message)));
            return AssertErrors(mic);
        }

        /// <summary>
        /// Asserts that a <see cref="ValidationException"/> was thrown during execution with the specified <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected <see cref="MessageItemCollection"/> collection.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public virtual TSelf AssertErrors(MessageItemCollection messages)
        {
            AssertException();
            if (Exception is ValidationException vex)
            {
                if (messages != null && !Expectations.ExpectationsExtensions.TryAreMessagesMatched(messages, vex.Messages, out var errorMessage))
                    Implementor.AssertFail(errorMessage);
            }
            else
                Implementor.AssertFail($"Expected Exception type '{typeof(ValidationException).Name}' not equal to actual '{Exception!.GetType().Name}'.");

            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that the execution was successful; i.e. there was no <see cref="Exception"/> thrown.
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