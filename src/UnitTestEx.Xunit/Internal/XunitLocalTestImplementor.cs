// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Threading;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides a <see cref="AsyncLocal{T}"/>-based proxy to an <see cref="XunitTestImplementor"/> instance.
    /// </summary>
    public sealed class XunitLocalTestImplementor() : Abstractions.TestFrameworkImplementor
    {
        private static readonly AsyncLocal<XunitTestImplementor?> _localImplementor = new();

        /// <summary>
        /// Sets the <see cref="AsyncLocal{T}"/> <see cref="XunitTestImplementor"/>.
        /// </summary>
        /// <param name="implementor">The <see cref="XunitTestImplementor"/>.</param>
        internal static void SetLocalImplementor(XunitTestImplementor implementor) => _localImplementor.Value = implementor;

        /// <summary>
        /// Gets the <see cref="AsyncLocal{T}"/> <see cref="XunitTestImplementor"/>.
        /// </summary>
        private static XunitTestImplementor? Implementor => _localImplementor.Value;

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => Implementor?.AssertAreEqual(expected, actual, message);

        /// <inheritdoc/>
        public override void AssertFail(string? message) => Implementor?.AssertFail(message);

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => Implementor?.AssertInconclusive(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => Implementor?.WriteLine(message);
    }
}