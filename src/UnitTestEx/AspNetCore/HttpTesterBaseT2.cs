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
    /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="Type"/> representing itself.</typeparam>
    public abstract class HttpTesterBase<TValue, TSelf> : HttpTesterBase, IHttpResponseExpectations<TSelf>, IResponseValueExpectations<TValue, TSelf>, IEventExpectations<TSelf> where TSelf : HttpTesterBase<TValue, TSelf>
    {
        private readonly HttpResponseExpectations _httpResponseExpectations;
        private readonly ResponseValueExpectations<TValue> _responseValueExpectations;
        private readonly EventExpectations _eventExpectations;

        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTesterBase(TesterBase owner, TestServer testServer) : base(owner, testServer)
        {
            _httpResponseExpectations = new HttpResponseExpectations(Owner);
            _responseValueExpectations = new ResponseValueExpectations<TValue>(Owner);
            _eventExpectations = new EventExpectations(Owner);
        }

        /// <inheritdoc/>
        HttpResponseExpectations IHttpResponseExpectations<TSelf>.HttpResponseExpectations => _httpResponseExpectations;

        /// <inheritdoc/>
        ResponseValueExpectations<TValue> IResponseValueExpectations<TValue, TSelf>.ResponseValueExpectations => _responseValueExpectations;

        /// <inheritdoc/>
        EventExpectations IEventExpectations<TSelf>.EventExpectations => _eventExpectations;

        /// <inheritdoc/>
        protected override void AssertExpectations(HttpResponseMessage res)
        {
            _httpResponseExpectations.Assert(HttpResult.CreateAsync(res).GetAwaiter().GetResult());
            _responseValueExpectations.Assert(HttpResponseExpectations.GetValueFromHttpResponseMessage<TValue>(res, JsonSerializer));
            _eventExpectations.Assert();
        }
    }
}