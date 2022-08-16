// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Http;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides <b>HTTP Agent</b> <see cref="TypedHttpClientBase"/> testing.
    /// </summary>
    public class AgentTester<TAgent> : HttpTesterBase<AgentTester<TAgent>> where TAgent : TypedHttpClientBase
    {
        /// <summary>
        /// Initializes a new <see cref="AgentTester{TAgent}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal AgentTester(TesterBase owner, TestServer testServer) : base(owner, testServer) { }

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(Func<TAgent, Task<HttpResult>> func) => base.RunAsync(func).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor<TValue> Run<TValue>(Func<TAgent, Task<HttpResult<TValue>>> func) => base.RunAsync<TAgent, TValue>(func).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(Func<TAgent, Task<HttpResponseMessage>> func) => base.RunAsync(func).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(Func<TAgent, Task<HttpResult>> func) => base.RunAsync(func);

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor<TValue>> RunAsync<TValue>(Func<TAgent, Task<HttpResult<TValue>>> func) => base.RunAsync(func);

        /// <summary>
        /// Runs the test by executing a <typeparamref name="TAgent"/> method.
        /// </summary>
        /// <param name="func">The function to execution.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync(Func<TAgent, Task<HttpResponseMessage>> func) => base.RunAsync(func);
    }
}