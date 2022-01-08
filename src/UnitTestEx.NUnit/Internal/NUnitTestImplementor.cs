// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    internal sealed class NUnitTestImplementor : Abstractions.TestFrameworkImplementor
    {
        /// <inheritdoc/>
        public override void AssertFail(string? message) => Assert.Fail(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T expected, T actual, string? message = null) => Assert.AreEqual(expected, actual, message, new object?[] { expected, actual });

        /// <inheritdoc/>
        public override void AssertIsType<TExpectedType>(object actual, string? message = null) => Assert.IsInstanceOf<TExpectedType>(actual, message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => TestContext.Out.WriteLine(message);

        /// <inheritdoc/>
        public override ILoggerProvider CreateLoggerProvider() => new NUnitLoggerProvider();

        /// <inheritdoc/>
        public override ILogger CreateLogger(string name) => new NUnitLogger(name);
    }
}