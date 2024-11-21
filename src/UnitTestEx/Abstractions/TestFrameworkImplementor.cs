// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using UnitTestEx.Logging;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the testing framework specific implementation.
    /// </summary>
    public abstract class TestFrameworkImplementor
    {
        private static Func<TestFrameworkImplementor>? _createFactory;
        private static readonly AsyncLocal<Func<TestFrameworkImplementor>?> _localCreateFactory = new();

        /// <summary>
        /// Sets the global <see cref="TestFrameworkImplementor"/> factory.
        /// </summary>
        /// <param name="createFactory">The function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public static void SetGlobalCreateFactory(Func<TestFrameworkImplementor> createFactory)
        {
            if (_createFactory is not null)
                throw new InvalidOperationException($"The global {nameof(TestFrameworkImplementor)} factory has already been set.");

            _createFactory = createFactory ?? throw new ArgumentNullException(nameof(createFactory));
        }

        /// <summary>
        /// Resets the global <see cref="TestFrameworkImplementor"/> factory.
        /// </summary>
        public static void ResetGlobalCreateFactory() => _createFactory = null;

        /// <summary>
        /// Sets the local <see cref="TestFrameworkImplementor"/> factory.
        /// </summary>
        /// <param name="createFactory">The function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public static void SetLocalCreateFactory(Func<TestFrameworkImplementor> createFactory)
        {
            if (_localCreateFactory.Value is not null)
                throw new InvalidOperationException($"The local {nameof(TestFrameworkImplementor)} factory has already been set.");

            _localCreateFactory.Value = createFactory ?? throw new ArgumentNullException(nameof(createFactory));
        }

        /// <summary>
        /// Resets the local <see cref="TestFrameworkImplementor"/> factory.
        /// </summary>
        public static void ResetLocalCreateFactory() => _localCreateFactory.Value = null;

        /// <summary>
        /// Creates a new instance of the <see cref="TestFrameworkImplementor"/> (see <see cref="SetGlobalCreateFactory"/> or <see cref="SetLocalCreateFactory"/>).
        /// </summary>
        /// <returns>The new instance of the <see cref="TestFrameworkImplementor"/>.</returns>
        public static TestFrameworkImplementor Create()
        {
            TestSetUp.Force();

            if (_localCreateFactory.Value is not null)
                return _localCreateFactory.Value.Invoke() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetLocalCreateFactory)} has returned null.");

            if (_createFactory is null)
                throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetGlobalCreateFactory)} must be set as a minimum.");

            return _createFactory?.Invoke() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetGlobalCreateFactory)} has returned null.");
        }

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