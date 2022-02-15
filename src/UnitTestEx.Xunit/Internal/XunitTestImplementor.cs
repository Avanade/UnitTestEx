// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>MSUnit</b> <see cref="Abstractions.TestFrameworkImplementor"/> implementation.
    /// </summary>
    internal sealed class XunitTestImplementor : Abstractions.TestFrameworkImplementor
    {
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitTestImplementor"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        public XunitTestImplementor(ITestOutputHelper output) => _output = output ?? throw new ArgumentNullException(nameof(output));

        /// <inheritdoc/>
        public override void AssertFail(string? message) => throw new XunitException(message);

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T expected, T actual, string? message = null) => Assert.Equal(expected, actual);

        /// <inheritdoc/>
        public override void AssertIsType<TExpectedType>(object actual, string? message = null) => Assert.IsAssignableFrom<TExpectedType>(actual);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => _output.WriteLine("{0}", message);

        /// <inheritdoc/>
        public override ILoggerProvider CreateLoggerProvider() => new XunitLoggerProvider(_output);

        /// <inheritdoc/>
        public override ILogger CreateLogger(string name) => new XunitLogger(_output, name);
    }
}
