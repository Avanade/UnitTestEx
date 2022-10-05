﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using NUnit.Framework;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    public sealed class NUnitTestImplementor : Abstractions.TestFrameworkImplementor
    {
        /// <summary>
        /// Creates a <see cref="NUnitTestImplementor"/> .
        /// </summary>
        /// <returns>The <see cref="NUnitTestImplementor"/>.</returns>
        public static NUnitTestImplementor Create() => new();

        /// <inheritdoc/>
        public override void AssertFail(string? message) => Assert.Fail(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => Assert.AreEqual(expected, actual, message, new object?[] { expected, actual });

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => Assert.Inconclusive(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => TestContext.Out.WriteLine(message);
    }
}