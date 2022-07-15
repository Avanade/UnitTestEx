// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the testing framework specific implementation.
    /// </summary>
    public abstract class TestFrameworkImplementor
    {
        /// <summary>
        /// Writes the <paramref name="message"/> as test output.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public abstract void WriteLine(string? message);

        /// <summary>
        /// Asserts whether the <paramref name="actual"/> <see cref="Type"/> is the same as the <typeparamref name="TExpectedType"/>.
        /// </summary>
        /// <typeparam name="TExpectedType">The expected <see cref="Type"/>.</typeparam>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">The optional failure message.</param>
        public abstract void AssertIsType<TExpectedType>(object actual, string? message = null);

        /// <summary>
        /// Asserts whether the <paramref name="actual"/> <see cref="Type"/> is the same as the <paramref name="expectedType"/>.
        /// </summary>
        /// <param name="expectedType">The expected <see cref="Type"/>.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">The optional failure message.</param>
        public abstract void AssertIsType(Type expectedType, object actual, string? message = null);

        /// <summary>
        /// Asserts whether the <paramref name="actual"/> and <paramref name="expected"/> value are equal.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">The optional failure message.</param>
        public abstract void AssertAreEqual<T>(T? expected, T? actual, string? message = null);

        /// <summary>
        /// Assert an immediate fail using the specified <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The failure message.</param>
        public abstract void AssertFail(string? message);

        /// <summary>
        /// Assert execution as inconclusive using the specified <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The inconclusive message.</param>
        public abstract void AssertInconclusive(string? message);

        /// <summary>
        /// Creates an <see cref="ILoggerProvider"/>.
        /// </summary>
        /// <returns>The <see cref="ILoggerProvider"/>.</returns>
        public abstract ILoggerProvider CreateLoggerProvider();

        /// <summary>
        /// Creates an <see cref="ILogger"/> instance with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The logger name.</param>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public abstract ILogger CreateLogger(string name);
    }
}