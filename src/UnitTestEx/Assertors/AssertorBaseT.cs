// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the base test assert helper that distinguishes between <see cref="AssertException"/> and <see cref="AssertSuccess"/>.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public abstract class AssertorBase<TSelf>(TesterBase owner, Exception? exception) : AssertorBase(owner, exception) where TSelf : AssertorBase<TSelf>
    {
        /// <summary>
        /// Asserts that an <see cref="Exception"/> was thrown during execution.
        /// </summary>
        /// <param name="expectedMessage">The optional expected message to match.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        public TSelf AssertException(string? expectedMessage = null)
        {
            if (Exception == null)
                Implementor.AssertFail("Expected an exception; however, the execution was successful.");
            else if (expectedMessage != null && expectedMessage != Exception.Message)
                Implementor.AssertAreEqual(expectedMessage, Exception.Message, $"Expected Exception message '{expectedMessage}' not equal to actual '{Exception.Message}'.");

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
        /// Asserts that an error occured with the following <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public TSelf AssertErrors(params string[] messages)
        {
            var errors = new List<ApiError>();
            foreach (var m in messages)
            {
                errors.Add(new ApiError(null, m));
            }

            return AssertErrors(errors.ToArray());
        }

        /// <summary>
        /// Asserts that an error occured with the following <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected <see cref="ApiError"/> collection.</param>
        /// <returns>The current instance to support fluent-style method-chaining.</returns>
        /// <remarks>Where not explicitly overridden will invoke the base <see cref="AssertorBase.AssertErrorsUsingExtensions(ApiError[])"/>.</remarks>
        public virtual TSelf AssertErrors(params ApiError[] errors)
        {
            AssertErrorsUsingExtensions(errors);
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