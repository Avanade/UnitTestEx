// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Http;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the base HTTP testing capabilities.
    /// </summary>
    /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="Type"/> representing itself.</typeparam>
    public abstract class HttpTesterBase<TResponse, TSelf> : HttpTesterBase where TSelf : HttpTesterBase<TResponse, TSelf>
    {
        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTesterBase(TesterBase owner, TestServer testServer) : base(owner, testServer) => Expectations = new HttpTestExpectations<TResponse, TSelf>(this);

        /// <summary>
        /// Gets the <see cref="HttpTestExpectations"/>.
        /// </summary>
        protected internal HttpTestExpectations<TResponse, TSelf> Expectations { get; }

        /// <inheritdoc/>
        protected override void AssertExpectations(HttpResponseMessage res) => Expectations.Assert(HttpResult.CreateAsync(res).GetAwaiter().GetResult());
    }
}