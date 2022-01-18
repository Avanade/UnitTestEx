// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    internal sealed class MSTestImplementor : Abstractions.TestFrameworkImplementor
    {
        /// <inheritdoc/>
        public override void AssertFail(string? message) => Assert.Fail(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T expected, T actual, string? message = null) => Assert.AreEqual(expected, actual, message, new object?[] { expected, actual });

        /// <inheritdoc/>
        public override void AssertIsType<TExpectedType>(object actual, string? message = null) => Assert.IsInstanceOfType(actual, typeof(TExpectedType), message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => Logger.LogMessage("{0}", message);

        /// <inheritdoc/>
        public override ILoggerProvider CreateLoggerProvider() => new MSTestLoggerProvider();

        /// <inheritdoc/>
        public override ILogger CreateLogger(string name) => new MSTestLogger(name);
    }
}