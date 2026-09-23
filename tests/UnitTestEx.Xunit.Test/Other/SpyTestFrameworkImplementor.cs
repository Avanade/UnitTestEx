#nullable enable

using System.Collections.Generic;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    /// <summary>
    /// A <see cref="TestFrameworkImplementor"/> that forwards to an <paramref name="inner"/> implementor (so the underlying test framework still receives all output), while also capturing every
    /// <see cref="WriteLine(string?)"/> call so tests can assert on the exact output produced (e.g. by <see cref="Abstractions.TesterBase{TSelf}"/>'s <c>Reason</c>/<c>WaitAndLog</c> methods).
    /// </summary>
    /// <param name="inner">The <see cref="TestFrameworkImplementor"/> to forward to.</param>
    internal sealed class SpyTestFrameworkImplementor(TestFrameworkImplementor inner) : TestFrameworkImplementor
    {
        /// <summary>
        /// Gets the captured lines written via <see cref="WriteLine(string?)"/>.
        /// </summary>
        public List<string?> Lines { get; } = [];

        /// <inheritdoc/>
        public override void WriteLine(string? message)
        {
            Lines.Add(message);
            inner.WriteLine(message);
        }

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => inner.AssertAreEqual(expected, actual, message);

        /// <inheritdoc/>
        public override void AssertFail(string? message) => inner.AssertFail(message);

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => inner.AssertInconclusive(message);
    }
}
