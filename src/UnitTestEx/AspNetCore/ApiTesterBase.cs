// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the basic API unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class ApiTesterBase<TEntryPoint, TSelf> : IDisposable where TEntryPoint : class where TSelf : ApiTesterBase<TEntryPoint, TSelf> 
    {
        private bool _disposed;
        private WebApplicationFactory<TEntryPoint>? _waf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected ApiTesterBase(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            _waf = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(whb => whb.UseSolutionRelativeContentRoot(Environment.CurrentDirectory));
        }

        /// <summary>
        /// Gets the <see cref="WebApplicationFactory{TEntryPoint}"/>.
        /// </summary>
        internal WebApplicationFactory<TEntryPoint> WebApplicationFactory => _waf!;

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ConfigureServices(Action<IServiceCollection> configureServices)
        {
            if (configureServices != null)
                _waf = WebApplicationFactory.WithWebHostBuilder(whb => whb.ConfigureServices(configureServices));

            return (TSelf)this;
        }

        /// <summary>
        /// Replace scoped service with a mock object.
        /// </summary>
        /// <typeparam name="T">The underlying <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockScopedService<T>(Mock<T> mock) where T : class => ConfigureServices(sc => sc.ReplaceScoped(mock.Object));

        /// <summary>
        /// Bypasses authorization by using the <see cref="BypassAuthorizationHandler"/> <see cref="IAuthorizationHandler"/>.
        /// </summary>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf BypassAuthorization() => ConfigureServices(sc => sc.ReplaceSingleton<IAuthorizationHandler>(new BypassAuthorizationHandler()));

        /// <summary>
        /// Specify the <see cref="ControllerBase">API Controller</see> to test.
        /// </summary>
        /// <typeparam name="TController">The API Controller <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ControllerTester{TController}"/>.</returns>
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => new(WebApplicationFactory.Server, Implementor);

        /// <summary>
        /// Releases all resources.
        /// </summary>
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

            if (_waf != null)
            {
                _waf.Dispose();
                _waf = null;
            }

            _disposed = true;
        }
    }
}