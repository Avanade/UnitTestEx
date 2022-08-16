// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="ExceptionSuccessExpectations"/>.
    /// </summary>
    /// <typeparam name="TSelf">The declaring <see cref="System.Type"/> itself.</typeparam>
    public interface IExceptionSuccessExpectations<TSelf> where TSelf : IExceptionSuccessExpectations<TSelf>
    {
        /// <summary>
        /// Gets the <see cref="Expectations.ExceptionSuccessExpectations"/>
        /// </summary>
        ExceptionSuccessExpectations ExceptionSuccessExpectations { get; }
    }
}