﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        private WebApplicationFactory<TEntryPoint>? _waf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public ApiTesterBase(TestFrameworkImplementor implementor) : base(implementor)
        {
            Logger = LoggerProvider.CreateLogger(GetType().Name);

            // Default the .NET environment environment variable.
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", TestSetUp.Environment);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", TestSetUp.Environment);

            // Add settings from appsettings.unittest.json so that they are available to the startup class.
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.unittest.json", optional: true)
                .Build();

            foreach (var key in config.AsEnumerable())
            {
                Environment.SetEnvironmentVariable(key.Key, key.Value);
            }
        }

        /// <summary>
        /// Gets the <see cref="WebApplicationFactory{TEntryPoint}"/>; instantiates on first access.
        /// </summary>
        protected WebApplicationFactory<TEntryPoint> GetWebApplicationFactory()
        {
            lock (SyncRoot)
            {
                if (_waf != null)
                    return _waf;

                return _waf = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(whb =>
                    whb.UseSolutionRelativeContentRoot(Environment.CurrentDirectory)
                        .ConfigureAppConfiguration((_, cb) =>
                        {
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);
                            if (AdditionalConfiguration != null)
                                cb.AddInMemoryCollection(AdditionalConfiguration);
                        })
                        .ConfigureServices(sc =>
                        {
                            sc.AddHttpContextAccessor();
                            SharedState.HttpContextAccessor = sc.BuildServiceProvider().GetRequiredService<IHttpContextAccessor>();

                            sc.ReplaceScoped(_ => SharedState);

                            foreach (var tec in TestSetUp.Extensions)
                                tec.ConfigureServices(this, sc);

                            SetUp.ConfigureServices?.Invoke(sc);
                            AddConfiguredServices(sc);
                        }).ConfigureLogging(lb => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); }));
            }
        }

        /// <inheritdoc/>
        protected override void ResetHost() => _waf = null;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public override IServiceProvider Services => GetWebApplicationFactory().Services;

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
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => new(this, GetTestServer());

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="HttpTester"/>.</returns>
        public HttpTester Http() => new(this, GetTestServer());

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/> with an expected response value <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="HttpTester{TResponse}"/>.</returns>
        public HttpTester<TResponse> Http<TResponse>() => new(this, GetTestServer());

        /// <summary>
        /// Specifies the <see cref="Type"/> of <typeparamref name="T"/> that is to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>() where T : class => new(this, HostExecutionWrapper(Services.CreateScope));

        /// <summary>
        /// Gets the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="TestServer"/>.</returns>
        public TestServer GetTestServer() => HostExecutionWrapper(() => GetWebApplicationFactory().Server);

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