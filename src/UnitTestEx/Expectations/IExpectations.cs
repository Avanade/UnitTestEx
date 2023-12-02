// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables the <b>Expectations</b> fluent-style method-chaining capabilities.
    /// </summary>
    /// <typeparam name="TSelf">The <see cref="System.Type"/> representing itself.</typeparam>
    public interface IExpectations<TSelf> where TSelf : IExpectations<TSelf>
    {
        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        ExpectationsArranger<TSelf> ExpectationsArranger { get; }
    }
}