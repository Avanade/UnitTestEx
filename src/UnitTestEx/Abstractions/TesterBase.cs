// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using UnitTestEx.Json;
using UnitTestEx.Logging;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common/core base unit-testing capabilities.
    /// </summary>
    public abstract class TesterBase
    {
        private string? _userName;
        private readonly List<Action<IServiceCollection>> _configureServices = [];

        /// <summary>
        /// Static constructor.
        /// </summary>
        static TesterBase()
        {
            try
            {
                var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "appsettings.unittest.json"));
                if (!fi.Exists)
                    return;

                var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fi.FullName));
                if (json.RootElement.TryGetProperty("DefaultJsonSerializer", out var je) && je.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (string.Compare(je.GetString(), "System.Text.Json", StringComparison.InvariantCultureIgnoreCase) == 0)
                        TestSetUp.Default.JsonSerializer = Json.JsonSerializer.Default = new Json.JsonSerializer();
                    else if (string.Compare(je.GetString(), "Newtonsoft.Json", StringComparison.InvariantCultureIgnoreCase) == 0)
                        TestSetUp.Default.JsonSerializer = Json.JsonSerializer.Default = new Json.Newtonsoft.JsonSerializer();
                }
            }
            catch { } // Swallow and carry on; none of this logic should impact execution.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public TesterBase(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            LoggerProvider = new SharedStateLoggerProvider(SharedState);
            SetUp = TestSetUp.Default.Clone();
            JsonSerializer = SetUp.JsonSerializer;
            JsonComparerOptions = SetUp.JsonComparerOptions;
            TestSetUp.LogAutoSetUpOutputs(Implementor);
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="SharedStateLoggerProvider"/> <see cref="ILoggerProvider"/>.
        /// </summary>
        public SharedStateLoggerProvider LoggerProvider { get; }

        /// <summary>
        /// Gets the <see cref="TestSharedState"/>.
        /// </summary>
        public TestSharedState SharedState { get; } = new TestSharedState();

        /// <summary>
        /// Gets the configured <see cref="TestSetUp"/>. 
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.Default"/>.</remarks>
        public TestSetUp SetUp { get; internal set; }

        /// <summary>
        /// Indicates whether the underlying host has been instantiated.
        /// </summary>
        /// <remarks>The host can be reset by invoking <see cref="TesterBase{TSelf}.ResetHost(bool)"/>.</remarks>
        public bool IsHostInstantiated { get; internal set; }

        /// <summary>
        /// Gets the synchronization object where synchronized access is required.
        /// </summary>
        protected object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets the test user name.
        /// </summary>
        /// <remarks>Defaults to <see cref="SetUp"/> <see cref="TestSetUp.DefaultUserName"/>.</remarks>
        public string UserName
        {
            get => _userName ?? SetUp.DefaultUserName;
            protected set => _userName = value;
        }

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        /// <remarks>Accessing the <see cref="Configuration"/> may result in the underlying host being instantiated (see <see cref="IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        /// <remarks>Accessing the <see cref="Services"/> may result in the underlying host being instantiated (see <see cref="IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IServiceProvider Services { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="TesterBase{TSelf}.UseJsonSerializer"/> method. This does <i>not</i> use the
        /// instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public IJsonSerializer JsonSerializer { get; internal set; }

        /// <summary>
        /// Gets the <see cref="JsonElementComparerOptions"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="TesterBase{TSelf}.UseJsonSerializer"/> method. This does <i>not</i> use the
        /// instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public JsonElementComparerOptions JsonComparerOptions { get; internal set; }

        /// <summary>
        /// Creates a <see cref="JsonElementComparer"/> using the configured <see cref="TesterBase.JsonComparerOptions"/> and <see cref="TesterBase.JsonSerializer"/>.
        /// </summary>
        /// <returns>A new <see cref="JsonElementComparer"/> instance.</returns>
        public JsonElementComparer CreateJsonComparer()
        {
            var options = JsonComparerOptions.Clone();
            options.JsonSerializer ??= JsonSerializer;
            return new JsonElementComparer(options);
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        /// <param name="resetConfiguredServices">Indicates whether to reset the previously configured services.</param>
        public void ResetHost(bool resetConfiguredServices = false)
        {
            lock (SyncRoot)
            {
                IsHostInstantiated = false;
                if (resetConfiguredServices)
                    _configureServices.Clear();

                ResetHost();
            }
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        protected abstract void ResetHost();

        /// <summary>
        /// Provides an opportunity to further configure the services before the underlying host is instantiated.
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the services.</param>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated. Internally, the <paramref name="configureServices"/> is queued and then played in order when the host is initially instantiated.
        /// Once instantiated, further calls will result in a <see cref="InvalidOperationException"/> unless a <see cref="ResetHost(bool)"/> is performed.</remarks>
        public void ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
        {
            lock (SyncRoot)
            {
                if (autoResetHost)
                    ResetHost(false);

                _configureServices.Add(configureServices);
            }
        }

        /// <summary>
        /// Adds the previously <see cref="ConfigureServices(Action{IServiceCollection}, bool)"/> to the <paramref name="services"/>.
        /// </summary>
        /// <remarks>It is recommended that this is performed within a <see cref="TesterBase.SyncRoot"/> to ensure thread-safety.</remarks>
        protected void AddConfiguredServices(IServiceCollection services)
        {
            if (IsHostInstantiated)
                throw new InvalidOperationException($"Underlying host has been instantiated and as such the {nameof(ConfigureServices)} operations can no longer be used.");

            foreach (var configureService in _configureServices)
            {
                configureService(services);
            }

            IsHostInstantiated = true;
        }
    }
}