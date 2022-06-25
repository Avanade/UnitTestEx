// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using System.Collections.Concurrent;
using UnitTestEx.Expectations;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a means to share state between the <see cref="TesterBase"/> and the corresponding execution.
    /// </summary>
    public sealed class TestSharedState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestSharedState"/> class.
        /// </summary>
        internal TestSharedState() { }

        /// <summary>
        /// Gets the event storage for the <see cref="ExpectedEventPublisher"/>.
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentQueue<EventData>> EventStorage { get; } = new();

        /// <summary>
        /// Resets the shared state.
        /// </summary>
        public void Reset() => EventStorage.Clear();
    }
}