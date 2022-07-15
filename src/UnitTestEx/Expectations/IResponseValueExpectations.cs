// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="ResponseValueExpectations"/>.
    /// </summary>
    /// <typeparam name="TValue">The response value <see cref="System.Type"/>.</typeparam>
    /// <typeparam name="TSelf">The declaring <see cref="System.Type"/> itself.</typeparam>
    public interface IResponseValueExpectations<TValue, TSelf> where TSelf : IResponseValueExpectations<TValue, TSelf>
    {
        /// <summary>
        /// Gets the <see cref="Expectations.ResponseValueExpectations{TValue}"/>.
        /// </summary>
        ResponseValueExpectations<TValue> ResponseValueExpectations { get; }
    }
}