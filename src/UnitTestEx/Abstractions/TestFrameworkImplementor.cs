// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.IO;
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
        private static readonly AsyncLocal<TestFrameworkImplementor?> _localImplementor = new();

        /// <summary>
        /// Gets a <see cref="NullTestImplementor"/> instance that does nothing.
        /// </summary>
        public static NullTestImplementor Null { get; } = new();

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
                return;

            _localCreateFactory.Value = createFactory ?? throw new ArgumentNullException(nameof(createFactory));
        }

        /// <summary>
        /// Resets the local <see cref="TestFrameworkImplementor"/> factory.
        /// </summary>
        public static void ResetLocalCreateFactory() => _localCreateFactory.Value = null;

        /// <summary>
        /// Sets the current asynchronous execution context's <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal static void SetLocalImplementor(TestFrameworkImplementor implementor) => _localImplementor.Value = implementor ?? throw new ArgumentNullException(nameof(implementor));

        /// <summary>
        /// Resets the current asynchronous execution context's <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        internal static void ResetLocalImplementor() => _localImplementor.Value = null;

        /// <summary>
        /// Creates a new instance of the <see cref="TestFrameworkImplementor"/> (see <see cref="SetGlobalCreateFactory"/> or <see cref="SetLocalCreateFactory"/>), using <paramref name="output"/> when framework detection creates the implementor.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <param name="output">The optional framework output helper, <see cref="TextWriter"/>, or <see cref="Action{T}"/> accepting a string.</param>
        /// <returns>The new instance of the <see cref="TestFrameworkImplementor"/>.</returns>
        public static TestFrameworkImplementor Create(Func<TestFrameworkImplementor>? createFactory = null, object? output = null)
        {
            TestSetUp.Force();

            if (createFactory is not null)
                return createFactory() ?? throw new InvalidOperationException($"The {nameof(createFactory)} has returned null.");

            if (_localImplementor.Value is not null)
                return _localImplementor.Value;

            if (_localCreateFactory.Value is not null)
                return _localCreateFactory.Value.Invoke() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetLocalCreateFactory)} has returned null.");

            if (_createFactory is not null)
                return _createFactory() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetGlobalCreateFactory)} has returned null.");

            return RuntimeTestFrameworkImplementor.Create(output);
        }

        /// <summary>
        /// Creates the automatically selected framework implementor using an optional per-test output object or callback.
        /// </summary>
        /// <param name="output">An optional framework output helper, <see cref="TextWriter"/>, or <see cref="Action{T}"/> accepting a string.</param>
        /// <returns>The framework implementor.</returns>
        public static TestFrameworkImplementor CreateForCurrentFramework(object? output = null)
        {
            TestSetUp.Force();
            return RuntimeTestFrameworkImplementor.Create(output);
        }

        /// <summary>
        /// Gets the active implementor for the current asynchronous context, falling back to framework detection.
        /// </summary>
        /// <returns>The active implementor.</returns>
        internal static TestFrameworkImplementor GetCurrentOrDefault()
        {
            if (_localImplementor.Value is not null)
                return _localImplementor.Value;

            if (_localCreateFactory.Value is not null)
                return _localCreateFactory.Value() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetLocalCreateFactory)} has returned null.");

            if (_createFactory is not null)
                return _createFactory() ?? throw new InvalidOperationException($"The {nameof(TestFrameworkImplementor)}.{nameof(SetGlobalCreateFactory)} has returned null.");

            return RuntimeTestFrameworkImplementor.Create();
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

    /// <summary>
    /// Provides a <see cref="TestFrameworkImplementor"/> that does nothing.
    /// </summary>
    public class NullTestImplementor : TestFrameworkImplementor
    {
        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default { }

        /// <inheritdoc/>
        public override void AssertFail(string? message) { }

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) { }

        /// <inheritdoc/>
        public override void WriteLine(string? message) { }
    }
}