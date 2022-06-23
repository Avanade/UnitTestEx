// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using UnitTestEx.Abstractions;
using UnitTestEx.Hosting;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the basic API unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class ApiTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable where TEntryPoint : class where TSelf : ApiTesterBase<TEntryPoint, TSelf> 
    {
        private bool _disposed;
        private WebApplicationFactory<TEntryPoint> _waf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="ExecutionContext.Current"/> <see cref="ExecutionContext.Username"/> where configured).</param>
        protected ApiTesterBase(TestFrameworkImplementor implementor, string? username) : base(implementor, username)
        {
            Logger = implementor.CreateLogger(GetType().Name);

            // add settings from appsettings.unittest.json so that they are available to the startup class
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.unittest.json")
                .Build();
            
            foreach (var key in config.AsEnumerable())
            {
                Environment.SetEnvironmentVariable(key.Key, key.Value);
            }

            _waf = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(whb => 
                whb.UseSolutionRelativeContentRoot(Environment.CurrentDirectory)
                    .ConfigureAppConfiguration((_, x) => x.AddJsonFile("appsettings.unittest.json", true))
                    .ConfigureServices(sc => sc.AddLogging(c => { c.ClearProviders(); c.AddProvider(implementor.CreateLoggerProvider()); })));
        }

        /// <summary>
        /// Gets the <see cref="WebApplicationFactory{TEntryPoint}"/>.
        /// </summary>
        internal WebApplicationFactory<TEntryPoint> WebApplicationFactory => _waf!;

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public override TSelf ConfigureServices(Action<IServiceCollection> configureServices)
        {
            if (configureServices != null)
                _waf = WebApplicationFactory.WithWebHostBuilder(whb => whb.ConfigureServices(configureServices));

            return (TSelf)this;
        }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public override IServiceProvider Services => _waf.Services;

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public override IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

        /// <summary>
        /// Gets the runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="ILogger"/> for the specified <typeparamref name="TCategoryName"/> from the underlying <see cref="Services"/>.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="Type"/> to infer the category name.</typeparam>
        /// <returns>The <see cref="ILogger{TCategoryName}"/>.</returns>
        public ILogger<TCategoryName> GetLogger<TCategoryName>() => Services.GetRequiredService<ILogger<TCategoryName>>();

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
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => new(this, WebApplicationFactory.Server);

        /// <summary>
        /// Enables an agent (<see cref="CoreEx.Http.TypedHttpClientBase"/>) to be used to send a <see cref="HttpRequestMessage"/> to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <typeparam name="TAgent">The <see cref="CoreEx.Http.TypedHttpClientBase"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="AgentTester{TAgent}"/></returns>
        public AgentTester<TAgent> Agent<TAgent>() where TAgent : CoreEx.Http.TypedHttpClientBase => new(this, WebApplicationFactory.Server);

        /// <summary>
        /// Enables an agent (<see cref="CoreEx.Http.TypedHttpClientBase"/>) to be used to send a <see cref="HttpRequestMessage"/> to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <typeparam name="TAgent">The <see cref="CoreEx.Http.TypedHttpClientBase"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="AgentTester{TAgent}"/></returns>
        public AgentTester<TAgent, TResponse> Agent<TAgent, TResponse>() where TAgent : CoreEx.Http.TypedHttpClientBase => new(this, WebApplicationFactory.Server);

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="HttpTester"/>.</returns>
        public HttpTester Http() => new(this, WebApplicationFactory.Server);

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/> with an expected response value <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="HttpTester{TResponse}"/>.</returns>
        public HttpTester<TResponse> Http<TResponse>() => new(this, WebApplicationFactory.Server);

        /// <summary>
        /// Specifies the <see cref="Type"/> of <typeparamref name="T"/> that is to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>() where T : class => new(Services.CreateScope(), Implementor, JsonSerializer);

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