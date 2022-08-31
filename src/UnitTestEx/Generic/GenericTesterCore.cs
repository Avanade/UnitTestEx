// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Configuration;
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
    /// <typeparam name="TSelf">The <see cref="GenericTesterCore{TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class GenericTesterCore<TSelf> : TesterBase<TSelf>, IDisposable, Expectations.IExceptionSuccessExpectations<TSelf> where TSelf : GenericTesterCore<TSelf>
    {
        private readonly IHostBuilder _hostBuilder;
        private IHost? _host;
        private bool _disposed;
        private readonly ExceptionSuccessExpectations _exceptionSuccessExpectations;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTesterCore{TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected GenericTesterCore(TestFrameworkImplementor implementor) : base(implementor)
        {
            Logger = implementor.CreateLogger(GetType().Name);
            _exceptionSuccessExpectations = new(this);

            _hostBuilder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureLogging((lb) => lb.AddProvider(implementor.CreateLoggerProvider()))
                .ConfigureAppConfiguration(cb =>
                {
                    cb.SetBasePath(Environment.CurrentDirectory)
                      .AddEnvironmentVariables()
                      .AddJsonFile("appsettings.unittest.json", optional: true);
                })
                .ConfigureServices(sc =>
                {
                    sc.AddExecutionContext(sp => { var ec = SetUp.ExecutionContextFactory(sp); ec.Username = UserName; return ec; });
                    sc.AddSettings<DefaultSettings>();
                    sc.ReplaceScoped(_ => SharedState);
                    SetUp.ConfigureServices?.Invoke(sc);
                    if (SetUp.ExpectedEventsEnabled)
                        ReplaceExpectedEventPublisher(sc);
                });
        }

        /// <summary>
        /// Gets the runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="IHost"/>.
        /// </summary>
        private IHost GetHost() => _host ??= _hostBuilder.Build();

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

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This will throw an <see cref="InvalidOperationException"/> once the underlying host has been created</remarks>
        public override TSelf ConfigureServices(Action<IServiceCollection> configureServices)
        {
            if (_host != null)
                throw new InvalidOperationException($"{nameof(ConfigureServices)} cannot be invoked after the test host has been created; as a result of executing a test, or using {nameof(Services)}, {nameof(GetLogger)} or {nameof(Configuration)}");

            if (configureServices != null)
                _hostBuilder.ConfigureServices(configureServices);

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

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            _disposed = true;
        }
    }
}