// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Net.Http;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables the <see cref="HttpResponseMessage"/> <b>Expectations</b> fluent-style method-chaining capabilities.
    /// </summary>
    /// <typeparam name="TSelf">The <see cref="System.Type"/> representing itself.</typeparam>
    public interface IHttpResponseMessageExpectations<TSelf> : IExpectations<TSelf> where TSelf : IHttpResponseMessageExpectations<TSelf> { }
}