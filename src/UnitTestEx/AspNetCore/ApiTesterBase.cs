// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private WebApplicationFactory<TEntryPoint> _waf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected ApiTesterBase(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            JsonSerializer = CoreEx.Json.JsonSerializer.Default;
            _waf = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(whb => whb.UseSolutionRelativeContentRoot("").ConfigureServices(sc => sc.AddLogging(c => { c.ClearProviders(); c.AddProvider(implementor.CreateLoggerProvider()); })));
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
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="CoreEx.Json.JsonSerializer.Default"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="UseJsonSerializer(IJsonSerializer)"/> method.</remarks>
        public IJsonSerializer JsonSerializer { get; private set; }

        /// <summary>
        /// Updates the <see cref="JsonSerializer"/> used by the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> itself, not the underlying executing host which should be configured separately.
        /// </summary>
        /// <param name="jsonSerializer">The <see cref="JsonSerializer"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseJsonSerializer(IJsonSerializer jsonSerializer)
        {
            JsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            return (TSelf)this;
        }

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
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public IServiceProvider Services => _waf.Services;

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

        /// <summary>
        /// Replace singleton service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockSingletonService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(_ => mock.Object));

        /// <summary>
        /// Replace scoped service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockScopedService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceScoped(_ => mock.Object));

        /// <summary>
        /// Replace transient service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockTransientService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceTransient(_ => mock.Object));

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
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => new(WebApplicationFactory.Server, Implementor, JsonSerializer);

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

            _waf.Dispose();
            _disposed = true;
        }
    }
}