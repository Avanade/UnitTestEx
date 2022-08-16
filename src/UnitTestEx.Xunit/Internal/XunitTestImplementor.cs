// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>Xunit</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    public sealed class XunitTestImplementor : Abstractions.TestFrameworkImplementor
    {
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Creates a <see cref="XunitTestImplementor"/> using the <paramref name="testOutputHelper"/>.
        /// </summary>
        /// <param name="testOutputHelper">The <see cref="ITestOutputHelper"/>.</param>
        /// <returns>The <see cref="XunitTestImplementor"/>.</returns>
        public static XunitTestImplementor Create(ITestOutputHelper testOutputHelper) => new(testOutputHelper);

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitTestImplementor"/> class.
        /// </summary>
        /// <param name="testOutputHelper">The <see cref="ITestOutputHelper"/>.</param>
        public XunitTestImplementor(ITestOutputHelper testOutputHelper) => _output = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));

        /// <inheritdoc/>
        public override void AssertFail(string? message) => throw new XunitException(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default => Assert.Equal(expected, actual);

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message) => AssertFail($"Inconclusive: {message}");

        /// <inheritdoc/>
        public override void WriteLine(string? message) => _output.WriteLine("{0}", message);

        /// <inheritdoc/>
        public override ILoggerProvider CreateLoggerProvider() => new XunitLoggerProvider(_output);

        /// <inheritdoc/>
        public override ILogger CreateLogger(string name) => new XunitLogger(_output, name);
    }
}
