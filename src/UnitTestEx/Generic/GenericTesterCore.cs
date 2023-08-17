﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Configuration;
using CoreEx.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides a basic genericcore  test host.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="GenericTesterCore{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class GenericTesterCore<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable, Expectations.IExceptionSuccessExpectations<TSelf>, Expectations.IEventExpectations<TSelf>, Expectations.ILoggerExpectations<TSelf>
        where TEntryPoint : IHostStartup, new() where TSelf : GenericTesterCore<TEntryPoint, TSelf>
    {
        private IHost? _host;
        private bool _disposed;
        private readonly ExceptionSuccessExpectations _exceptionSuccessExpectations;
        private readonly EventExpectations _eventExpectations;
        private readonly LoggerExpectations _loggerExpectations;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTesterCore{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected GenericTesterCore(TestFrameworkImplementor implementor) : base(implementor)
        {
            Logger = LoggerProvider.CreateLogger(GetType().Name);
            _exceptionSuccessExpectations = new(implementor);
            _eventExpectations = new(this);
            _loggerExpectations = new(implementor);
        }

        /// <summary>
        /// Gets the runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="IHost"/>.
        /// </summary>
        private IHost GetHost()
        {
            lock (SyncRoot)
            {
                var ep = new TEntryPoint();

                return _host ??= new HostBuilder()
                    .UseEnvironment(TestSetUp.Environment)
                    .ConfigureLogging((lb) => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                    .ConfigureHostConfiguration(ep.ConfigureHostConfiguration)
                    .ConfigureAppConfiguration((hbc, cb) =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory)
                          .AddJsonFile("appsettings.json", optional: true)
                          .AddJsonFile($"appsettings.{TestSetUp.Environment.ToLowerInvariant()}.json", optional: true)
                          .AddEnvironmentVariables();

                        ep.ConfigureAppConfiguration(hbc, cb);

                        cb.AddJsonFile("appsettings.unittest.json", optional: true);
                    })
                    .ConfigureServices(sc =>
                    {
                        ep.ConfigureServices(sc);
                        sc.AddExecutionContext(sp => { var ec = SetUp.ExecutionContextFactory(sp); ec.UserName = UserName; return ec; });
                        sc.AddSettings<DefaultSettings>();
                        sc.ReplaceScoped(_ => SharedState);
                        SetUp.ConfigureServices?.Invoke(sc);
                        AddConfiguredServices(sc);
                    }).Build();
            }
        }

        /// <inheritdoc/>
        protected override void ResetHost() => _host = null;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public override IServiceProvider Services => GetHost().Services;

        /// <summary>
        /// Gets the <see cref="ILogger"/> for the specified <typeparamref name="TCategoryName"/> from the underlying <see cref="Services"/>.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="Type"/> to infer the category name.</typeparam>
        /// <returns>The <see cref="ILogger{TCategoryName}"/>.</returns>
        public ILogger<TCategoryName> GetLogger<TCategoryName>() => Services.GetRequiredService<ILogger<TCategoryName>>();

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying <see cref="Services"/>.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public override IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

        /// <summary>
        /// Gets the <see cref="SettingsBase"/> from the underlying host.
        /// </summary>
        public override SettingsBase Settings => Services.GetService<SettingsBase>() ?? new DefaultSettings(Configuration);

        /// <inheritdoc/>
        public ExceptionSuccessExpectations ExceptionSuccessExpectations => _exceptionSuccessExpectations;

        /// <inheritdoc/>
        public EventExpectations EventExpectations => _eventExpectations;

        /// <inheritdoc/>
        public LoggerExpectations LoggerExpectations => _loggerExpectations;

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

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            _disposed = true;
        }
    }
}