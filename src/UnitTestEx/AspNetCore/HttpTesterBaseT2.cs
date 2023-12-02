// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the base HTTP testing capabilities.
    /// </summary>
    /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="Type"/> representing itself.</typeparam>
    public abstract class HttpTesterBase<TValue, TSelf> : HttpTesterBase, IHttpResponseMessageExpectations<TSelf>, IValueExpectations<TValue, TSelf> where TSelf : HttpTesterBase<TValue, TSelf>
    {
        /// <summary>
        /// Initializes a new <see cref="HttpTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        public HttpTesterBase(TesterBase owner, TestServer testServer) : base(owner, testServer) => ExpectationsArranger = new ExpectationsArranger<TSelf>(owner, (TSelf)this);

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<TSelf> ExpectationsArranger { get; }

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
        /// Sets (overrides) the test user name (defaults to <see cref="TesterBase.UserName"/>).
        /// </summary>
        /// <param name="userIdentifier">The test user identifier.</param>
        /// <returns>The <typeparamref name="TSelf"/> instance to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="TestSetUp.UserNameConverter"/> is required for the conversion to take place.</remarks>
        public TSelf WithUser(object? userIdentifier)
        {
            if (userIdentifier == null)
                return WithUser(null);

            if (Owner.SetUp.UserNameConverter == null)
                throw new InvalidOperationException($"The {nameof(TestSetUp)}.{nameof(TestSetUp.UserNameConverter)} must be defined to support user identifier conversion.");

            return WithUser(Owner.SetUp.UserNameConverter(userIdentifier));
        }

        /// <inheritdoc/>
        protected override Task AssertExpectationsAsync(HttpResponseMessage res) => ExpectationsArranger.AssertAsync(ExpectationsArranger.CreateArgs(LastLogs).AddExtra(res));
    }
}