// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NFI = NUnit.Framework.Internal;
using UnitTestEx.Logging;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Represents the <see cref="NUnitLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    public sealed class NUnitLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;
        private readonly NFI.TestExecutionContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="NUnitLoggerProvider"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NFI.TestExecutionContext"/>.</param>
        public NUnitLoggerProvider(NFI.TestExecutionContext context) => _context = context;

        /// <summary>
        /// Creates a new instance of the <see cref="NUnitLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="NUnitLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new NUnitLogger(_context, name, _scopeProvider);

        /// <summary>
        /// Closes and disposes the <see cref="NUnitLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc/>
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Represents an <b>NUnit</b> <see cref="ILogger"/> that writes to <see cref="TestContext.Out"/>.
    /// </summary>
    public sealed class NUnitLogger : LoggerBase
    {
        private readonly NFI.TestExecutionContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="NUnitLogger"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NFI.TestExecutionContext"/>.</param>
        /// <param name="name">The name of the logger.</param>
        /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
        public NUnitLogger(NFI.TestExecutionContext context, string name, IExternalScopeProvider? scopeProvider = null) : base(name, scopeProvider) => _context = context;

        /// <inheritdoc />
        protected override void WriteMessage(string message) => _context.OutWriter.WriteLine(message);
    }
}