// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the basic API unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="System.Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class ApiTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IHttpClientSource, IDisposable where TEntryPoint : class where TSelf : ApiTesterBase<TEntryPoint, TSelf> 
    {
        private sealed class UnitTestExWebApplicationFactory(string contentRoot, string configurationDirectory, Action<IWebHostBuilder> configure) : WebApplicationFactory<TEntryPoint>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseSolutionRelativeContentRoot(contentRoot);
                configure(builder);
            }

            protected override IHost CreateHost(IHostBuilder builder)
            {
                return ApiTesterEnvironment.CreateHost(configurationDirectory, () => base.CreateHost(builder));
            }
        }

        private bool _disposed;
        private WebApplicationFactory<TEntryPoint>? _waf;
        private string? _solutionRelativePath;
        private readonly string _configurationDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public ApiTesterBase(TestFrameworkImplementor implementor) : base(implementor)
        {
            _configurationDirectory = Environment.CurrentDirectory;
            Logger = LoggerProvider.CreateLogger(GetType().Name);

        }

        /// <summary>
        /// Gets the <see cref="WebApplicationFactory{TEntryPoint}"/>; instantiates on first access.
        /// </summary>
        protected WebApplicationFactory<TEntryPoint> GetWebApplicationFactory()
        {
            if (_waf != null)
                return _waf;

            lock (SyncRoot)
            {
                if (_waf != null)
                    return _waf;

                var contentRoot = _solutionRelativePath ?? Environment.CurrentDirectory;
                _waf = new UnitTestExWebApplicationFactory(contentRoot, _configurationDirectory, whb =>
                    whb
                        .UseEnvironment(TestSetUp.Environment)
                        .ConfigureAppConfiguration((_, cb) =>
                        {
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);
                            if (AdditionalConfiguration != null)
                                cb.AddInMemoryCollection(AdditionalConfiguration);
                        })
                        .ConfigureServices(sc =>
                        {
                            var httpContextAccessor = new HttpContextAccessor();
                            sc.AddHttpContextAccessor();
                            sc.ReplaceSingleton<IHttpContextAccessor>(httpContextAccessor);
                            SharedState.HttpContextAccessor = httpContextAccessor;

                            sc.ReplaceScoped(_ => SharedState);

                            foreach (var tec in TestSetUp.Extensions)
                                tec.ConfigureServices(this, sc);

                            SetUp.ConfigureServices?.Invoke(sc);
                            AddConfiguredServices(sc);
                        }).ConfigureLogging(lb => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); }));

                OnHostStartUp();

                return _waf;
            }
        }

        /// <inheritdoc/>
        protected override void OnResetHost()
        {
            lock (SyncRoot)
            {
                if (_waf is not null)
                {
                    _waf.Dispose();
                    _waf = null;
                    Implementor.WriteLine("");
                    Implementor.WriteLine("** The underlying UnitTestEx 'ApiTester' Host has been reset. **");
                    Implementor.WriteLine("");
                }
            }
        }

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
        /// <typeparam name="TCategoryName">The <see cref="System.Type"/> to infer the category name.</typeparam>
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
        /// <typeparam name="TController">The API Controller <see cref="System.Type"/>.</typeparam>
        /// <returns>The <see cref="ControllerTester{TController}"/>.</returns>
        public ControllerTester<TController> Controller<TController>() where TController : ControllerBase => new(this, this);

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="HttpTester"/>.</returns>
        public HttpTester Http() => new(this, this);

        /// <summary>
        /// Enables a test <see cref="HttpRequestMessage"/> to be sent to the underlying <see cref="TestServer"/> with an expected response value <see cref="System.Type"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="System.Type"/>.</typeparam>
        /// <returns>The <see cref="HttpTester{TResponse}"/>.</returns>
        public HttpTester<TResponse> Http<TResponse>() => new(this, this);

        /// <summary>
        /// Gets the underlying <see cref="TestServer"/>.
        /// </summary>
        /// <returns>The <see cref="TestServer"/>.</returns>
        public TestServer GetTestServer() => HostExecutionWrapper(() => GetWebApplicationFactory().Server);

        /// <inheritdoc/>
        /// <remarks>Deliberately bypasses <see cref="GetTestServer"/> (and therefore its <see cref="TestSharedState.Reset"/> side-effect) here: this is invoked per-request, potentially <i>after</i>
        /// expectations (e.g. <c>ExpectEvents</c>) have already registered state into <see cref="TestSharedState.StateData"/> for the current request's <see cref="HttpTesterBase.RequestId"/>. Routing
        /// through the resetting wrapper on every request wipes that state immediately before the request is sent, silently discarding registered expectation flags (causing false "no events published"
        /// failures). <see cref="GetTestServer"/> itself is left untouched for its other (construction-time, pre-expectation) callers.</remarks>
        public HttpClient CreateHttpClient(string? name = null)
        {
            var server = GetWebApplicationFactory().Server;
            return new HttpClient(server.CreateHandler()) { BaseAddress = server.BaseAddress };
        }


        /// <summary>
        /// Sets the content root to be relative to the solution directory (i.e. the directory containing the .sln file). 
        /// </summary>
        /// <param name="solutionRelativePath">The directory of the solution file.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This is required when the API project is not in the same directory as the test project and ensures that the API project's appsettings.json files are found and used.
        /// <para>This is the functional equivalent of <see cref="Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.UseSolutionRelativeContentRoot(Microsoft.AspNetCore.Hosting.IWebHostBuilder, string, string)"/>.</para></remarks>
        public TSelf UseSolutionRelativeContentRoot(string? solutionRelativePath)
        {
            if (_waf != null)
                throw new InvalidOperationException("The content root must be set before the WebApplicationFactory is instantiated.");

            _solutionRelativePath = solutionRelativePath;
            return (TSelf)this;
        }

        /// <summary>
        /// Adds an action to be executed before the underlying test <b>Run</b> occurs.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf AddPreRunAction(Action<IExpectations> action)
        {
            if (action is not null)
                PreRunActions.Add(tester => action(tester));

            return (TSelf)this;
        }

        /// <summary>
        /// Adds an action to be to be executed after the underlying test <b>Run</b> occurs (before <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf AddPostRunBeforeExpectationsAction(Action<IExpectations> action)
        {
            if (action is not null)
                PostRunBeforeExpectationsActions.Add(tester => action(tester));

            return (TSelf)this;
        }

        /// <summary>
        /// Adds an action to be to be executed after the underlying test <b>Run</b> occurs (after <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf AddPostRunAfterExpectationsAction(Action<IExpectations> action)
        {
            if (action is not null)
                PostRunAfterExpectationsActions.Add(tester => action(tester));

            return (TSelf)this;
        }

        /// <summary>
        /// Adds an action to be executed after the underlying test <b>Run</b> occurs (after all other post-run actions regardless of result).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf AddPostRunAction(Action<IExpectations> action)
        {
            if (action is not null)
                PostRunActions.Add(tester => action(tester));

            return (TSelf)this;
        }

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

            _waf?.Dispose();
            _waf = null;

            _disposed = true;
        }
    }

    internal static class ApiTesterEnvironment
    {
        private static readonly object _lock = new();

        public static IHost CreateHost(string contentRoot, Func<IHost> createHost)
        {
            // Entry points may inspect configuration before WebApplicationFactory applies its test configuration sources.
            lock (_lock)
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(contentRoot)
                    .AddJsonFile("appsettings.unittest.json", optional: true)
                    .Build();

                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DOTNET_ENVIRONMENT"] = TestSetUp.Environment,
                    ["ASPNETCORE_ENVIRONMENT"] = TestSetUp.Environment
                };

                foreach (var pair in configuration.AsEnumerable())
                {
                    if (pair.Value is not null)
                        values[pair.Key] = pair.Value;
                }

                var previous = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in values.Keys)
                    previous[key] = Environment.GetEnvironmentVariable(key);

                try
                {
                    foreach (var pair in values)
                    {
                        if (!string.IsNullOrEmpty(pair.Key))
                            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                    }

                    return createHost();
                }
                finally
                {
                    foreach (var pair in previous)
                    {
                        if (!string.IsNullOrEmpty(pair.Key))
                            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                    }
                }
            }
        }
    }
}