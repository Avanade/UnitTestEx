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
    public abstract class HttpTesterBase<TSelf> : HttpTesterBase, IExceptionSuccessExpectations<TSelf>, IHttpResponseExpectations<TSelf>, IEventExpectations<TSelf> where TSelf : HttpTesterBase<TSelf>
    {
        private readonly ExceptionSuccessExpectations _exceptionSuccessExpectations;
        private readonly HttpResponseExpectations _httpResponseExpectations;
        private readonly EventExpectations _eventExpectations;

        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal HttpTesterBase(TesterBase owner, TestServer testServer) : base(owner, testServer)
        {
            _exceptionSuccessExpectations = new ExceptionSuccessExpectations(Owner);
            _httpResponseExpectations = new HttpResponseExpectations(Owner);
            _eventExpectations = new EventExpectations(Owner);
        }

        /// <inheritdoc/>
        ExceptionSuccessExpectations IExceptionSuccessExpectations<TSelf>.ExceptionSuccessExpectations => _exceptionSuccessExpectations;

        /// <inheritdoc/>
        HttpResponseExpectations IHttpResponseExpectations<TSelf>.HttpResponseExpectations => _httpResponseExpectations;

        /// <inheritdoc/>
        EventExpectations IEventExpectations<TSelf>.EventExpectations => _eventExpectations;

        /// <summary>
        /// Sets (overrides) the test user name (defaults to <see cref="TesterBase.UserName"/>).
        /// </summary>
        /// <param name="userName">The test user name.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        public TSelf WithUser(string? userName)
        {
            UserName = userName;
            return (TSelf)this;
        }

        /// <summary>
        /// Perform the assertion of any expectations.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>/</param>
        protected override void AssertExpectations(HttpResponseMessage res)
        {
            var hr = HttpResult.CreateAsync(res).GetAwaiter().GetResult();
            try 
            {
                hr.ThrowOnError(true, true);
                _exceptionSuccessExpectations.Assert(null);
            }
            catch (AggregateException aex)
            {
                _exceptionSuccessExpectations.Assert(aex.InnerException ?? aex);
            }
            catch (Exception ex)
            {
                _exceptionSuccessExpectations.Assert(ex);
            }

            _httpResponseExpectations.Assert(hr);
            _eventExpectations.Assert();
        }
    }
}