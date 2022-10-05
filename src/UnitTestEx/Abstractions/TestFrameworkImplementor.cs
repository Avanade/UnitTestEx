// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using UnitTestEx.Logging;

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
        public ILoggerProvider CreateLoggerProvider() => new TestFrameworkImplementorLoggerProvider(this);
    }
}