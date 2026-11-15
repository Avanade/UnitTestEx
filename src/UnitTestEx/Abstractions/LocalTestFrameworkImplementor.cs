// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides an asynchronous-context proxy to the active test framework implementor.
    /// </summary>
    internal sealed class LocalTestFrameworkImplementor : TestFrameworkImplementor
    {
        /// <inheritdoc/>
        public override void WriteLine(string? message) => TestFrameworkImplementor.GetCurrentOrDefault().WriteLine(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => TestFrameworkImplementor.GetCurrentOrDefault().AssertAreEqual(expected, actual, message);

        /// <inheritdoc/>
        public override void AssertFail(string? message) => TestFrameworkImplementor.GetCurrentOrDefault().AssertFail(message);

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => TestFrameworkImplementor.GetCurrentOrDefault().AssertInconclusive(message);
    }
}
