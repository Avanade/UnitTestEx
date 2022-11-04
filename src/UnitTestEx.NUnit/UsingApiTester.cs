// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using UnitTestEx.AspNetCore;
using UnitTestEx.Hosting;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Provides a shared <see cref="ApiTester"/> instance for all tests.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <remarks>Implements <see cref="IDisposable"/> so should be automatically disposed off by the test framework host.</remarks>
    public abstract class UsingApiTester<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsingApiTester{TEntryPoint}"/> class.
        /// </summary>
        protected UsingApiTester() => ApiTester = NUnit.ApiTester.Create<TEntryPoint>();

        /// <summary>
        /// Gets the <see cref="Internal.ApiTester{TEntryPoint}"/>.
        /// </summary>
        public Internal.ApiTester<TEntryPoint> ApiTester { get; }

        /// <summary>
        /// Specify the <see cref="ControllerBase">API Controller</see> to test.
        /// </summary>
        /// <typeparam name="TController">The API Controller <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ControllerTester{TController}"/>.</returns>
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => ApiTester.Controller<TController>();

        /// <summary>
        /// Enables an agent (<see cref="CoreEx.Http.TypedHttpClientBase"/>) to be used to send a <see cref="HttpRequestMessage"/> to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <typeparam name="TAgent">The <see cref="CoreEx.Http.TypedHttpClientBase"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="AgentTester{TAgent}"/></returns>
        public AgentTester<TAgent> Agent<TAgent>() where TAgent : CoreEx.Http.TypedHttpClientBase => ApiTester.Agent<TAgent>();

        /// <summary>
        /// Enables an agent (<see cref="CoreEx.Http.TypedHttpClientBase"/>) to be used to send a <see cref="HttpRequestMessage"/> to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TAgent">The <see cref="CoreEx.Http.TypedHttpClientBase"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="AgentTester{TAgent}"/></returns>
        public AgentTester<TAgent, TResponse> Agent<TAgent, TResponse>() where TAgent : CoreEx.Http.TypedHttpClientBase => ApiTester.Agent<TAgent, TResponse>();

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="HttpTester"/>.</returns>
        public HttpTester Http() => ApiTester.Http();

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/> with an expected response value <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="HttpTester{TResponse}"/>.</returns>
        public HttpTester<TResponse> Http<TResponse>() => ApiTester.Http<TResponse>();

        /// <summary>
        /// Specifies the <see cref="Type"/> of <typeparamref name="T"/> that is to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>() where T : class => ApiTester.Type<T>();

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            ApiTester.Dispose();
            _disposed = true;
        }
    }
}