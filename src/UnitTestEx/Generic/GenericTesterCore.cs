// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides a basic generic test host.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="GenericTesterCore{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class GenericTesterCore<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable, IExpectations<TSelf>
        where TEntryPoint : class where TSelf : GenericTesterCore<TEntryPoint, TSelf>
    {
        private IHost? _host;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTesterCore{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public GenericTesterCore(TestFrameworkImplementor implementor) : base(implementor)
        {
            Logger = LoggerProvider.CreateLogger(GetType().Name);
            ExpectationsArranger = new ExpectationsArranger<TSelf>(this, (TSelf)this);
        }

        /// <summary>
        /// Gets the runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<TSelf> ExpectationsArranger { get; }

        /// <summary>
        /// Gets the <see cref="IHost"/>.
        /// </summary>
        private IHost GetHost()
        {
            if (_host is not null)
                return _host;

            lock (SyncRoot)
            {
                if (_host is not null)
                    return _host;

                var ep = new EntryPoint(Activator.CreateInstance<TEntryPoint>());

#if NET8_0_OR_GREATER
                var settings = new HostApplicationBuilderSettings
                {
                    EnvironmentName = TestSetUp.Environment,
                    ContentRootPath = Environment.CurrentDirectory
                };

                var builder = Host.CreateApplicationBuilder(settings);
                builder.Logging.SetMinimumLevel(SetUp.MinimumLogLevel).ClearProviders().AddProvider(LoggerProvider);

                if (ep.HasConfigureHostConfiguration)
                    ep.ConfigureHostConfiguration(builder.Configuration);

                builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                                     .AddJsonFile($"appsettings.{TestSetUp.Environment.ToLowerInvariant()}.json", optional: true);

                if (ep.HasConfigureAppConfiguration)
                {
                    var fi = builder.GetType().GetField("_hostBuilderContext", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fi is not null)
                    {
                        var hbc = (HostBuilderContext)fi.GetValue(builder)!;
                        ep.ConfigureAppConfiguration(hbc, builder.Configuration);
                    }
                }

                builder.Configuration.AddJsonFile("appsettings.unittest.json", optional: true)
                                     .AddEnvironmentVariables();

                if (AdditionalConfiguration != null)
                    builder.Configuration.AddInMemoryCollection(AdditionalConfiguration);

                if (ep.HasConfigureServices)
                    ep.ConfigureServices(builder.Services);

                if (ep.HasConfigureApplication)
                    ep.ConfigureApplication(builder);

                builder.Services.ReplaceScoped(_ => SharedState);

                foreach (var tec in TestSetUp.Extensions)
                    tec.ConfigureServices(this, builder.Services);

                SetUp.ConfigureServices?.Invoke(builder.Services);
                AddConfiguredServices(builder.Services);

                _host = builder.Build();
                OnHostStartUp();
                return _host;
#else
                _host ??= Host.CreateDefaultBuilder()
                    .UseEnvironment(TestSetUp.Environment)
                    .ConfigureLogging((lb) => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                    .ConfigureHostConfiguration(cb =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory);
                        ep.ConfigureHostConfiguration(cb);
                        cb.AddJsonFile("appsettings.json", optional: true)
                          .AddJsonFile($"appsettings.{TestSetUp.Environment.ToLowerInvariant()}.json", optional: true);
                    })
                    .ConfigureAppConfiguration((hbc, cb) =>
                    {
                        ep.ConfigureAppConfiguration(hbc, cb);
                        cb.AddJsonFile("appsettings.unittest.json", optional: true)
                          .AddEnvironmentVariables();

                        if (AdditionalConfiguration != null)
                            cb.AddInMemoryCollection(AdditionalConfiguration);
                    })
                    .ConfigureServices(sc =>
                    {
                        ep.ConfigureServices(sc);
                        sc.ReplaceScoped(_ => SharedState);

                        foreach (var tec in TestSetUp.Extensions)
                            tec.ConfigureServices(this, sc);

                        SetUp.ConfigureServices?.Invoke(sc);
                        AddConfiguredServices(sc);
                    }).Build();
                
                OnHostStartUp();
                return _host;
#endif
            }
        }

        /// <inheritdoc/>
        protected override void ResetHost()
        {
            lock (SyncRoot)
            {
                if (_host is not null)
                {
                    _host.Dispose();
                    _host = null;
                    Implementor.WriteLine("");
                    Implementor.WriteLine("** The underlying UnitTestEx 'GenericTester' Host has been reset. **");
                    Implementor.WriteLine("");
                }
            }
        }

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