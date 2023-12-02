// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables the <b>Expectations</b> fluent-style method-chaining capabilities.
    /// </summary>
    /// <typeparam name="TValue">The response value <see cref="System.Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="System.Type"/> representing itself.</typeparam>
    public interface IValueExpectations<TValue, TSelf> : IExpectations<TSelf> where TSelf : IValueExpectations<TValue, TSelf> { } 
}