// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <see cref="HttpResponseExpectations"/>.
    /// </summary>
    /// <typeparam name="TSelf">The declaring <see cref="System.Type"/> itself.</typeparam>
    public interface IHttpResponseExpectations<TSelf> where TSelf : IHttpResponseExpectations<TSelf>
    {
        /// <summary>
        /// Gets the <see cref="Expectations.HttpResponseExpectations"/>.
        /// </summary>
        HttpResponseExpectations HttpResponseExpectations { get; }
    }
}