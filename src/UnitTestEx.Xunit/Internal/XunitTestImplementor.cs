// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>Xunit</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    /// <param name="testOutputHelper">The <see cref="ITestOutputHelper"/>.</param>
    public sealed class XunitTestImplementor(ITestOutputHelper testOutputHelper) : Abstractions.TestFrameworkImplementor
    {
        private const string _notSpecifiedText = "Not specified.";

        private readonly ITestOutputHelper _output = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));

        /// <summary>
        /// Creates a <see cref="XunitTestImplementor"/> using the <paramref name="testOutputHelper"/>.
        /// </summary>
        /// <param name="testOutputHelper">The <see cref="ITestOutputHelper"/>.</param>
        /// <returns>The <see cref="XunitTestImplementor"/>.</returns>
        public static XunitTestImplementor Create(ITestOutputHelper testOutputHelper) => new(testOutputHelper);

        /// <inheritdoc/>
        public override void AssertFail(string? message) => throw new XunitException(message ?? _notSpecifiedText);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default
        {
            try
            {
                Assert.Equal(expected, actual);
            }
            catch (XunitException ex)
            {
                throw new XunitException(message is null ? ex.Message : $"{message}{Environment.NewLine}{ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => AssertFail($"Inconclusive: {message}");

        /// <inheritdoc/>
        public override void WriteLine(string? message) => _output.WriteLine("{0}", message);
    }
}