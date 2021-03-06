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
    /// <typeparam name="TSelf">The <see cref="Type"/> representing itself.</typeparam>
    public abstract class HttpTesterBase<TSelf> : HttpTesterBase, IHttpResponseExpectations<TSelf>, IEventExpectations<TSelf> where TSelf : HttpTesterBase<TSelf>
    {
        private readonly HttpResponseExpectations _httpResponseExpectations;
        private readonly EventExpectations _eventExpectations;

        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTesterBase(TesterBase owner, TestServer testServer) : base(owner, testServer)
        {
            _httpResponseExpectations = new HttpResponseExpectations(Owner);
            _eventExpectations = new EventExpectations(Owner);
        }

        /// <inheritdoc/>
        HttpResponseExpectations IHttpResponseExpectations<TSelf>.HttpResponseExpectations => _httpResponseExpectations;

        /// <inheritdoc/>
        EventExpectations IEventExpectations<TSelf>.EventExpectations => _eventExpectations;

        /// <summary>
        /// Perform the assertion of any expectations.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>/</param>
        protected override void AssertExpectations(HttpResponseMessage res)
        {
            _httpResponseExpectations.Assert(HttpResult.CreateAsync(res).GetAwaiter().GetResult());
            _eventExpectations.Assert();
        }
    }
}