// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="EventExpectations"/>.
    /// </summary>
    /// <typeparam name="TSelf">The <see cref="System.Type"/> representing itself.</typeparam>
    public interface IEventExpectations<TSelf> where TSelf : IEventExpectations<TSelf>
    {
        /// <summary>
        /// Gets the <see cref="Expectations.EventExpectations"/>.
        /// </summary>
        public EventExpectations EventExpectations { get; }
    }
}