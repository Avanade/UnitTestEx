// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    public sealed class MSTestImplementor : Abstractions.TestFrameworkImplementor
    {
        /// <summary>
        /// Creates a <see cref="MSTestImplementor"/>.
        /// </summary>
        /// <returns>The <see cref="MSTestImplementor"/>.</returns>
        public static MSTestImplementor Create() => new();

        /// <inheritdoc/>
        public override void AssertFail(string? message) => Assert.Fail(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => Assert.AreEqual(expected, actual, message, new object?[] { expected, actual });

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => Assert.Inconclusive(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => Logger.LogMessage("{0}", message);
    }
}