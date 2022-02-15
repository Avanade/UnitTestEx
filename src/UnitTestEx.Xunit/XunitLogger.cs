// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using UnitTestEx.Logging;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit
{
    /// <summary>
    /// Represents the <see cref="XunitLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    public sealed class XunitLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitLoggerProvider"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        public XunitLoggerProvider(ITestOutputHelper output) => _output = output ?? throw new ArgumentNullException(nameof(output));

        /// <summary>
        /// Creates a new instance of the <see cref="XunitLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="XunitLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new XunitLogger(_output, name, _scopeProvider);

        /// <summary>
        /// Closes and disposes the <see cref="XunitLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc/>
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Represents an <b>Xunit</b> <see cref="ILogger"/> that uses <see cref="ITestOutputHelper.WriteLine(string)"/>.
    /// </summary>
    public sealed class XunitLogger : LoggerBase
    {
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitLogger"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        /// <param name="name">The name of the logger.</param>
        /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
        public XunitLogger(ITestOutputHelper output, string name, IExternalScopeProvider? scopeProvider = null) : base(name, scopeProvider) => _output = output ?? throw new ArgumentNullException(nameof(output));

        /// <inheritdoc />
        protected override void WriteMessage(string message) => _output.WriteLine(message);
    }
}
