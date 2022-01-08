// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System.Diagnostics;
using UnitTestEx.Logging;

namespace UnitTestEx.MSUnit
{
    /// <summary>
    /// Represents the <see cref="MSTestLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    [DebuggerStepThrough]
    public sealed class MSTestLoggerProvider : ILoggerProvider
    {
        /// <summary>
        /// Creates a new instance of the <see cref="MSTestLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="MSTestLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new MSTestLogger(name);

        /// <summary>
        /// Closes and disposes the <see cref="MSTestLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// Represents an <b>MSTest</b> <see cref="ILogger"/> that writes to <see cref="Microsoft.Extensions.Logging.Logger"/>.
    /// </summary>
    [DebuggerStepThrough]
    public sealed class MSTestLogger : LoggerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestLogger"/> class.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        public MSTestLogger(string name) : base(name) { }

        /// <inheritdoc />
        protected override void WriteMessage(string message) => Logger.LogMessage("{0}", message);
    }
}