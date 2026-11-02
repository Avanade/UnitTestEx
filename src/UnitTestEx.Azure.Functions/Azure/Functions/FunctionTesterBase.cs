// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Hosting;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides the basic Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IFunctionTesterOptions, IDisposable where TEntryPoint : class where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        private static readonly JsonDocumentOptions _localSettingsJsonDocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private readonly bool? _includeUnitTestConfiguration;
        private readonly bool? _includeUserSecrets;
        private readonly List<IServiceScope> _triggerScopes = [];
        private IHost? _host;
        private bool _disposed;
        private bool _useWorkerMiddleware = true;

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
        /// Reads the local.settings.json values into memory.
        /// </summary>
        private static List<KeyValuePair<string, string?>> GetLocalSettingsValues()
        {
            var file = new FileInfo(Path.Combine(Environment.CurrentDirectory, "local.settings.json"));
            if (!file.Exists)
                return [];

            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName), _localSettingsJsonDocumentOptions);
            if (!document.RootElement.TryGetProperty("Values", out var values) || values.ValueKind != JsonValueKind.Object)
                return [];

            return values.EnumerateObject()
                .Select(property => new KeyValuePair<string, string?>(
                    property.Name,
                    property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString()))
                .ToList();
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
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_host != null)
                    return _host;

                _host = BuildApplicationHost();

                OnHostStartUp();

                return _host;
            }
        }

        /// <summary>
        /// Builds the <see cref="IHost"/> by executing the application's own entry point (e.g. <c>Program.cs</c> top-level statements) as-is, capturing the host at <c>Build()</c> and applying the test configuration; the worker is never started.
        /// </summary>
        private IHost BuildApplicationHost()
        {
            return FunctionHostFactory.BuildHost(typeof(TEntryPoint).Assembly, [], UnitTestEx.TestSetUp.Environment, hb => hb
                .ConfigureLogging(lb => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                .ConfigureAppConfiguration((_, cb) =>
                {
                    cb.SetBasePath(Environment.CurrentDirectory).AddInMemoryCollection(GetLocalSettingsValues());
                    ConfigureTestAppConfiguration(cb);
                })
                .ConfigureServices(ConfigureTestServices));
        }

        /// <summary>
        /// Adds the unit-testing specific application configuration.
        /// </summary>
        private void ConfigureTestAppConfiguration(IConfigurationBuilder cb)
        {
            if (!_includeUserSecrets.HasValue && TestSetUp.FunctionTesterIncludeUserSecrets || _includeUserSecrets.HasValue && _includeUserSecrets.Value)
                cb.AddUserSecrets(typeof(TEntryPoint).Assembly, optional: true);

            cb.AddEnvironmentVariables();

            if (!_includeUnitTestConfiguration.HasValue && TestSetUp.FunctionTesterIncludeUnitTestConfiguration || _includeUnitTestConfiguration.HasValue && _includeUnitTestConfiguration.Value)
                cb.AddJsonFile("appsettings.unittest.json", optional: true);

            if (AdditionalConfiguration != null)
                cb.AddInMemoryCollection(AdditionalConfiguration);
        }

        /// <summary>
        /// Adds the unit-testing specific services (these override any existing registrations).
        /// </summary>
        private void ConfigureTestServices(IServiceCollection sc)
        {
            sc.ReplaceScoped(_ => SharedState);

            foreach (var tec in UnitTestEx.TestSetUp.Extensions)
                tec.ConfigureServices(this, sc);

            SetUp.ConfigureServices?.Invoke(sc);
            AddConfiguredServices(sc);
            FunctionPipeline.ConfigureServices(sc);
        }

        /// <summary>
        /// Indicates whether the function is executed within the application's isolated worker middleware pipeline; defaults to <c>true</c>.
        /// </summary>
        bool IFunctionTesterOptions.UseWorkerMiddleware => _useWorkerMiddleware;

        /// <summary>
        /// Indicates whether the function is to be executed within the application's isolated worker middleware pipeline (as registered via <c>UseMiddleware</c>, <c>UseWhen</c>, etc.); defaults to <c>true</c>.
        /// </summary>
        /// <param name="useWorkerMiddleware"><c>true</c> to execute within the worker middleware pipeline; otherwise, <c>false</c> to invoke the function method directly.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseWorkerMiddleware(bool useWorkerMiddleware = true)
        {
            _useWorkerMiddleware = useWorkerMiddleware;
            return (TSelf)this;
        }

        /// <inheritdoc/>
        protected override void OnResetHost()
        {
            lock (SyncRoot)
            {
                if (_host is not null)
                {
                    DisposeHost();
                    Implementor.WriteLine("");
                    Implementor.WriteLine("** The underlying UnitTestEx 'FunctionTester' Host has been reset. **");
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
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="HttpTriggerTester{TFunction}"/>.</returns>
        public HttpTriggerTester<TFunction> HttpTrigger<TFunction>() where TFunction : class => new(this, CreateTriggerScope());

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="ServiceBusTriggerTester{TFunction}"/>.</returns>
        public ServiceBusTriggerTester<TFunction> ServiceBusTrigger<TFunction>() where TFunction : class => new(this, CreateTriggerScope());

        /// <summary>
        /// Specifies an isolated-worker function with any trigger that does not have a dedicated tester.
        /// </summary>
        /// <typeparam name="TFunction">The function type.</typeparam>
        /// <returns>A generic trigger tester.</returns>
        public TriggerTester<TFunction> Trigger<TFunction>() where TFunction : class => new(this, CreateTriggerScope());

        /// <summary>
        /// Creates a trigger scope owned by this tester until the host is reset or disposed.
        /// </summary>
        private IServiceScope CreateTriggerScope()
        {
            lock (SyncRoot)
            {
                var scope = HostExecutionWrapper(() => GetHost().Services.CreateScope());
                _triggerScopes.Add(scope);
                return scope;
            }
        }

        /// <summary>
        /// Releases all trigger scopes before their host, reporting failures after attempting all cleanup.
        /// </summary>
        private void DisposeHost()
        {
            var scopes = _triggerScopes.ToArray();
            _triggerScopes.Clear();
            var host = _host;
            _host = null;
            List<Exception> exceptions = [];

            for (var i = scopes.Length - 1; i >= 0; i--)
            {
                try
                {
                    if (scopes[i] is IAsyncDisposable asyncScope)
                    {
                        // Keep the synchronous tester API usable with async-only services, without capturing the caller's context.
                        Task.Run(async () => await asyncScope.DisposeAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
                    }
                    else
                        scopes[i].Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            try
            {
                host?.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Count == 1)
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            else if (exceptions.Count > 1)
                throw new AggregateException("Failed to dispose the FunctionTester scopes and host.", exceptions);
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
            lock (SyncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                if (disposing)
                    DisposeHost();
            }
        }
    }
}