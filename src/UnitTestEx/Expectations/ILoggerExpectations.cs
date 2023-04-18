// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="LoggerExpectations"/>.
    /// </summary>
    /// <typeparam name="TSelf">The declaring <see cref="System.Type"/> itself.</typeparam>
    public interface ILoggerExpectations<TSelf> where TSelf : ILoggerExpectations<TSelf>
    {
        /// <summary>
        /// Gets the <see cref="Expectations.LoggerExpectations"/>.
        /// </summary>
        LoggerExpectations LoggerExpectations { get; }
    }
}