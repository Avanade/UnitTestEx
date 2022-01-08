// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Diagnostics;
using UnitTestEx.Logging;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Represents the <see cref="NUnitLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    [DebuggerStepThrough]
    public sealed class NUnitLoggerProvider : ILoggerProvider
    {
        /// <summary>
        /// Creates a new instance of the <see cref="NUnitLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="NUnitLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new NUnitLogger(name);

        /// <summary>
        /// Closes and disposes the <see cref="NUnitLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// Represents an <b>NUnit</b> <see cref="ILogger"/> that writes to <see cref="TestContext.Out"/>.
    /// </summary>
    [DebuggerStepThrough]
    public sealed class NUnitLogger : LoggerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NUnitLogger"/> class.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        public NUnitLogger(string name) : base(name) { }

        /// <inheritdoc />
        protected override void WriteMessage(string message) => TestContext.Out.WriteLine(message);
    }
}