// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Logging
{
    /// <summary>
    /// Represents the <see cref="SharedStateLogger"/> provider.
    /// </summary>
    [ProviderAlias("")]
    public sealed class SharedStateLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;
        private readonly TestSharedState _sharedState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedStateLoggerProvider"/> class.
        /// </summary>
        /// <param name="sharedState">The <see cref="TestSharedState"/>.</param>
        public SharedStateLoggerProvider(TestSharedState sharedState) => _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));

        /// <summary>
        /// Creates a new instance of the <see cref="SharedStateLogger"/>.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>The <see cref="SharedStateLogger"/>.</returns>
        public ILogger CreateLogger(string name) => new SharedStateLogger(_sharedState, name, _scopeProvider);

        /// <summary>
        /// Closes and disposes the <see cref="SharedStateLoggerProvider"/>.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc/>
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Provides an <see cref="ILogger"/> that writes to the <see cref="TestSharedState"/>.
    /// </summary>
    public class SharedStateLogger : LoggerBase
    {
        private readonly TestSharedState _sharedState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedStateLogger"/> class.
        /// </summary>
        /// <param name="sharedState">The <see cref="TestSharedState"/>.</param>
        /// <param name="name">The name of the logger.</param>
        /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
        public SharedStateLogger(TestSharedState sharedState, string name, IExternalScopeProvider? scopeProvider = null) : base(name, scopeProvider) => _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));

        /// <inheritdoc/>
        protected override void WriteMessage(string message) => _sharedState.AddLoggerMessage(message);
    }
}