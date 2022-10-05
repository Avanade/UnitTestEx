// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnitTestEx.Expectations;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a means to share state between the <see cref="TesterBase"/> and the corresponding execution.
    /// </summary>
    public sealed class TestSharedState
    {
        private readonly object _lock = new();
        private readonly List<string?> _logOutput = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSharedState"/> class.
        /// </summary>
        internal TestSharedState() { }

        /// <summary>
        /// Gets the event storage for the <see cref="ExpectedEventPublisher"/>.
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentQueue<EventData>> EventStorage { get; } = new();

        /// <summary>
        /// Adds the <see cref="ILogger"/> log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        public void AddLoggerMessage(string? message)
        {
            lock (_lock)
            {
                _logOutput.Add(message);
            }
        }

        /// <summary>
        /// Gets the <see cref="ILogger"/> messages.
        /// </summary>
        /// <remarks>Also clears the messages.</remarks>
        public IEnumerable<string?> GetLoggerMessages()
        {
            lock (_lock)
            {
                var logs = _logOutput.ToArray();
                _logOutput.Clear();
                return logs;
            }
        }

        /// <summary>
        /// Resets the <see cref="EventStorage"/> shared state.
        /// </summary>
        public void ResetEventStorage() => EventStorage.Clear();
    }
}