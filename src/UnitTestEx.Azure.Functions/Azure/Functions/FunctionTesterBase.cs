// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using UnitTestEx.Abstractions;
using UnitTestEx.Hosting;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides the basic Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="FunctionsStartup"/> or other entry point <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable where TEntryPoint : class where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        private static readonly object _lock = new();
        private static readonly JsonSerializerOptions _localSettingsJsonSerializerOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        private static bool _localSettingsDone = false;

        private readonly bool? _includeUnitTestConfiguration;
        private readonly bool? _includeUserSecrets;
        private IHost? _host;
        private bool _disposed;

        private class Fhb(IServiceCollection services) : IFunctionsHostBuilder
        {
            public IServiceCollection Services { get; } = services;
        }

        private class Fcb(IConfigurationBuilder configurationBuilder) : IFunctionsConfigurationBuilder
        {
            public IConfigurationBuilder ConfigurationBuilder { get; } = configurationBuilder;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        public FunctionTesterBase(TestFrameworkImplementor implementor, bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration) : base(implementor)
        {
            Logger = LoggerProvider.CreateLogger(GetType().Name);
            _includeUnitTestConfiguration = includeUnitTestConfiguration;
            _includeUserSecrets = includeUserSecrets;
            AdditionalConfiguration = additionalConfiguration;
        }

        /// <summary>
        /// Mock the <see cref="IFunctionsConfigurationBuilder"/> interface.
        /// </summary>
        private static IFunctionsConfigurationBuilder MockIFunctionsConfigurationBuilder(IConfigurationBuilder configurationBuilder)
        {
            var mock = new Mock<IFunctionsConfigurationBuilder>();
            mock.Setup(x => x.ConfigurationBuilder).Returns(configurationBuilder);
            return mock.Object;
        }

        /// <summary>
        /// Mock the <see cref="IFunctionsHostBuilder"/> interface.
        /// </summary>
        private static IFunctionsHostBuilder MockIFunctionsHostBuilder(IServiceCollection services)
        {
            var mock = new Mock<IFunctionsHostBuilder>();
            mock.Setup(x => x.Services).Returns(services);
            return mock.Object;
        }

        /// <summary>
        /// Get the local.settings.json values and store in a temporary file.
        /// </summary>
        private static string GetLocalSettingsJson()
        {
            lock (_lock)
            {
                // Manage a temporary local.settings.json for the values.
                var tfi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "temporary.local.settings.json"));
                if (tfi.Exists)
                {
                    if (_localSettingsDone)
                        return tfi.Name;

                    tfi.Delete();
                }

                // Simulate the loading of the local.settings.json values.
                var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "local.settings.json"));
                if (!fi.Exists)
                    return tfi.Name;

                var json = File.ReadAllText(fi.FullName);
                var je = (JsonElement)System.Text.Json.JsonSerializer.Deserialize<dynamic>(json, _localSettingsJsonSerializerOptions);
                if (je.TryGetProperty("Values", out var jv))
                {
                    using var fs = tfi.OpenWrite();
                    using var uw = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
                    jv.WriteTo(uw);
                    uw.Flush();
                }

                _localSettingsDone = true;
                return tfi.Name;
            }
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
            if (_host != null)
                return _host;

            lock (SyncRoot)
            {
                if (_host != null)
                    return _host;

                var ep = Activator.CreateInstance<TEntryPoint>();
                var ep2 = ep as FunctionsStartup;
                var ep3 = new EntryPoint(ep);

                return _host = new HostBuilder()
                    .UseEnvironment(UnitTestEx.TestSetUp.Environment)
                    .ConfigureLogging((lb) => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                    .ConfigureHostConfiguration(cb =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory)
                            .AddInMemoryCollection([new("AzureWebJobsConfigurationSection", "AzureFunctionsJobHost")])
                            .AddJsonFile(GetLocalSettingsJson(), optional: true)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile($"appsettings.{UnitTestEx.TestSetUp.Environment.ToLowerInvariant()}.json", optional: true);

                        ep3?.ConfigureHostConfiguration(cb);
                    })
                    .ConfigureAppConfiguration((hbc, cb) =>
                    {
                        ep2?.ConfigureAppConfiguration(MockIFunctionsConfigurationBuilder(cb));
                        ep3?.ConfigureAppConfiguration(hbc, cb);

                        if (!_includeUserSecrets.HasValue && TestSetUp.FunctionTesterIncludeUserSecrets || _includeUserSecrets.HasValue && _includeUserSecrets.Value)
                            cb.AddUserSecrets<TEntryPoint>();

                        cb.AddEnvironmentVariables();

                        if (!_includeUnitTestConfiguration.HasValue && TestSetUp.FunctionTesterIncludeUnitTestConfiguration || _includeUnitTestConfiguration.HasValue && _includeUnitTestConfiguration.Value)
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);

                        if (AdditionalConfiguration != null)
                            cb.AddInMemoryCollection(AdditionalConfiguration);
                    })
                    .ConfigureServices(sc =>
                    {
                        ep2?.Configure(MockIFunctionsHostBuilder(sc));
                        ep3?.ConfigureServices(sc);
                        sc.ReplaceScoped(_ => SharedState);

                        foreach (var tec in UnitTestEx.TestSetUp.Extensions)
                            tec.ConfigureServices(this, sc);

                        SetUp.ConfigureServices?.Invoke(sc);
                        AddConfiguredServices(sc);
                    }).Build();
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
                    Implementor.WriteLine("The underlying UnitTestEx 'FunctionTester' Host has been reset.");
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
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="HttpTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="HttpTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="HttpTriggerTester{TFunction}"/>.</returns>
        public HttpTriggerTester<TFunction> HttpTrigger<TFunction>() where TFunction : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()));

        /// <summary>
        /// Enables a specified <see cref="Type"/> (of <typeparamref name="T"/>) to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>(object? serviceKey = null) where T : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()), serviceKey);

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="ServiceBusTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="ServiceBusTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="ServiceBusTriggerTester{TFunction}"/>.</returns>
        public ServiceBusTriggerTester<TFunction> ServiceBusTrigger<TFunction>() where TFunction : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()));

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