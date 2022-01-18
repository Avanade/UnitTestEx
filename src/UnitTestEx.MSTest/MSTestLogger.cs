// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using UnitTestEx.Logging;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Represents the <see cref="MSTestLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    public sealed class MSTestLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="MSTestLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="MSTestLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new MSTestLogger(name, _scopeProvider);

        /// <summary>
        /// Closes and disposes the <see cref="MSTestLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc/>
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Represents an <b>MSTest</b> <see cref="ILogger"/> that writes to <see cref="Microsoft.Extensions.Logging.Logger"/>.
    /// </summary>
    public sealed class MSTestLogger : LoggerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestLogger"/> class.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
        public MSTestLogger(string name, IExternalScopeProvider? scopeProvider = null) : base(name, scopeProvider) { }

        /// <inheritdoc />
        protected override void WriteMessage(string message) => Logger.LogMessage("{0}", message);
    }
}