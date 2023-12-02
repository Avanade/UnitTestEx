// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Logging
{
    /// <summary>
    /// Represents the <see cref="TestFrameworkImplementorLogger"/> provider.
    /// </summary>
    /// <param name="testFrameworkImplementor">The <see cref="TestFrameworkImplementor"/>.</param>
    [ProviderAlias("")]
    public sealed class TestFrameworkImplementorLoggerProvider(TestFrameworkImplementor testFrameworkImplementor) : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;
        private readonly TestFrameworkImplementor _testFrameworkImplementor = testFrameworkImplementor ?? throw new ArgumentNullException(nameof(testFrameworkImplementor));

        /// <summary>
        /// Creates a new instance of the <see cref="TestFrameworkImplementorLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="TestFrameworkImplementorLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new TestFrameworkImplementorLogger(_testFrameworkImplementor, name, _scopeProvider);

        /// <summary>
        /// Closes and disposes the <see cref="TestFrameworkImplementorLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc/>
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Provides an <see cref="ILogger"/> that writes to the <see cref="TestFrameworkImplementor"/>.
    /// </summary>
    /// <param name="testFrameworkImplementor">The <see cref="TestFrameworkImplementor"/>.</param>
    /// <param name="name">The name of the logger.</param>
    /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
    public class TestFrameworkImplementorLogger(TestFrameworkImplementor testFrameworkImplementor, string name, IExternalScopeProvider? scopeProvider = null) : LoggerBase(name, scopeProvider)
    {
        private readonly TestFrameworkImplementor _testFrameworkImplementor = testFrameworkImplementor ?? throw new ArgumentNullException(nameof(testFrameworkImplementor));

        /// <inheritdoc/>
        protected override void WriteMessage(string message) => _testFrameworkImplementor.WriteLine(message);
    }
}