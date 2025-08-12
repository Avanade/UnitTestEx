// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx
{
    /// <summary>
    /// Orchestrates the set up for testing and provides default test settings.
    /// </summary>
    public class TestSetUp : ICloneable
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static readonly ConcurrentQueue<string> _autoSetUpOutputs = new();

        private TestSetUp? _clonedFrom;
        private bool _setUpSet = false;
        private int _setUpCount;
        private Func<int, object?, CancellationToken, Task<bool>>? _setUpFunc;
        private Func<int, object?, CancellationToken, Task<(bool, string?)>>? _autoSetUpFunc;

        #region Static

        /// <summary>
        /// Static constructor.
        /// </summary>
        /// <remarks>Wires up the <see cref="OneOffTestSetUpAttribute.SetUp()"/> invocation whenever an <see cref="Assembly"/> is <see cref="AppDomain.AssemblyLoad">loaded.</see></remarks>
        static TestSetUp()
        {
            // Load dependent UnitTestEx assemblies as they may not have been loaded yet!
            foreach (var fi in new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).EnumerateFiles("*.dll").Where(f => f.Name.StartsWith("UnitTestEx.")))
                Assembly.LoadFrom(fi.FullName);

            // Wire up for any assemblies already loaded.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                OneOffTestSetUpAttribute.SetUp(assembly);

            // Wire up for any future assembly loading.
            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => OneOffTestSetUpAttribute.SetUp(e.LoadedAssembly);
        }

        /// <summary>
        /// Forces the underlying static set up on first access.
        /// </summary>
        internal static void Force() { }

        /// <summary>
        /// Gets or sets the default <see cref="TestSetUp"/>.
        /// </summary>
        public static TestSetUp Default { get; set; } = new TestSetUp();

        /// <summary>
        /// Gets or sets the environment used for loading the likes of configuration files.
        /// </summary>
        /// <remarks>Sets the <c>DOTNET_ENVIRONMENT</c> environment variable; for more information <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0#default-host-configuration"/>.
        /// Defaults to '<c>Development</c>'.</remarks>
        public static string Environment { get; set; } = "Development";

        /// <summary>
        /// Gets or sets the <see cref="Task.Delay(int)"/> milliseconds to allow a host to finalize/dispose before expecting/asserting result.
        /// </summary>
        public static int TaskDelayMilliseconds { get; set; } = 1;

        /// <summary>
        /// Gets the <see cref="TesterExtensionsConfig"/> collection.
        /// </summary>
        /// <remarks>These are statically defined extension opportunities that allow additional capabilities to be included within the testing as if implemented natively, minimizing the need to inherit/override, etc. It is intended that
        /// the extensions would be registered as a one-off set up (see <see cref="OneOffTestSetUpBase"/>).</remarks>
        public static List<TesterExtensionsConfig> Extensions { get; } = [];

        /// <summary>
        /// Builds and gets a new <see cref="IConfiguration"/> from the: '<c>appsettings.unittest.json</c>', environment variables (using optional <paramref name="environmentVariablePrefix"/>), and command-line arguments.
        /// </summary>
        /// <param name="environmentVariablePrefix">The prefix that the environment variables must start with (will automatically add a trailing underscore where not supplied).</param>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        /// <remarks>The is built outside of any host and therefore host specific configuration is not available.</remarks>
        public static IConfiguration GetConfiguration(string? environmentVariablePrefix = null)
        {
            var cb = new ConfigurationBuilder()
                .SetBasePath(System.Environment.CurrentDirectory)
                .AddJsonFile("appsettings.unittest.json", optional: true);

            if (environmentVariablePrefix == null)
                cb.AddEnvironmentVariables();
            else
                cb.AddEnvironmentVariables(environmentVariablePrefix.EndsWith('_') ? environmentVariablePrefix : environmentVariablePrefix + "_");

            cb.AddCommandLine(System.Environment.GetCommandLineArgs());
            return cb.Build();
        }

        /// <summary>
        /// Gets the <see cref="AutoSetUpFunc"/> result output from the beginning of the internal queue.
        /// </summary>
        /// <returns>The output text from the beginning of the internal queue; where <c>null</c> this indicates no further outputs currently remain queued.</returns>
        public static string? GetAutoSetUpOutput() => _autoSetUpOutputs.TryDequeue(out var o) ? o : null;

        /// <summary>
        /// Logs the output from any previously executed <see cref="RegisterAutoSetUp"/> executions (see <see cref="GetAutoSetUpOutput"/>).
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public static void LogAutoSetUpOutputs(TestFrameworkImplementor implementor)
        {
            // Top-level test dividing line!
            implementor.WriteLine("");
            implementor.WriteLine(new string('=', 80));

            // Output any previously registered auto set up outputs.
            var output = GetAutoSetUpOutput();
            while (!string.IsNullOrEmpty(output))
            {
                implementor.WriteLine("");
                implementor.WriteLine("TEST-SET-UP OUTPUT >");
                implementor.WriteLine(output);
                implementor.WriteLine(new string('=', 80));
                output = GetAutoSetUpOutput();
            }
        }

        #endregion

        /// <summary>
        /// Gets or sets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer { get; set; } = Json.JsonSerializer.Default;

        /// <summary>
        /// Gets or sets the <see cref="JsonElementComparerOptions"/>.
        /// </summary>
        public JsonElementComparerOptions JsonComparerOptions { get; set; } = JsonElementComparerOptions.Default;

        /// <summary>
        /// Gets the dictionary for additional properties related to test set up.
        /// </summary>
        public Dictionary<string, object?> Properties { get; private set; } = [];

        /// <summary>
        /// Gets or sets the default user name.
        /// </summary>
        /// <remarks>Defaults to '<c>Anonymous</c>'.</remarks>
        public string DefaultUserName { get; set; } = "Anonymous";

        /// <summary>
        /// Gets or sets the function to convert a non-<see cref="string"/> user name to a <see cref="string"/> equivalent.
        /// </summary>
        public Func<object, string>? UserNameConverter { get; set; }

        /// <summary>
        /// Defines an <b>ETag</b> value that <i>should</i> result in a concurrency error.
        /// </summary>
        /// <remarks>Defaults to '<c>ZZZZZZZZZZZZ</c>'.</remarks>
        public string ConcurrencyErrorETag { get; set; } = "ZZZZZZZZZZZZ";

        /// <summary>
        /// Gets or sets the <see cref="Action"/> that enables the <see cref="IServiceCollection"/> to be updated before each execution.
        /// </summary>
        public Action<IServiceCollection>? ConfigureServices { get; set; }

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.UserName"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequestMessage, string?, CancellationToken, Task>? OnBeforeHttpRequestMessageSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequest"/> to be updated before each send.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.UserName"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequest, string?, CancellationToken, Task>? OnBeforeHttpRequestSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the minimum <see cref="LogLevel"/> when configuring the underlying host.
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

        /// <inheritdoc/>
        /// <remarks>The <see cref="RegisterSetUp"/> and <see cref="RegisterAutoSetUp"/> will reference the originating unless explicitly registered (overridden) for the cloned instance.</remarks>
        object ICloneable.Clone() => Clone();

        /// <summary>
        /// Creates a new instance that is a copy of the current instance.
        /// </summary>
        /// <remarks>The <see cref="RegisterSetUp"/> and <see cref="RegisterAutoSetUp"/> will reference the originating unless explicitly registered (overridden) for the cloned instance.</remarks>
        public TestSetUp Clone() => new()
        {
            JsonSerializer = JsonSerializer,
            JsonComparerOptions = JsonComparerOptions,
            Properties = new Dictionary<string, object?>(Properties),
            DefaultUserName = DefaultUserName,
            UserNameConverter = UserNameConverter,
            ConcurrencyErrorETag = ConcurrencyErrorETag,
            ConfigureServices = ConfigureServices,
            OnBeforeHttpRequestMessageSendAsync = OnBeforeHttpRequestMessageSendAsync,
            OnBeforeHttpRequestSendAsync = OnBeforeHttpRequestSendAsync,
            MinimumLogLevel = MinimumLogLevel,
            _clonedFrom = this
        };

        /// <summary>
        /// Registers the <paramref name="setUpFunc"/> that will be executed whenever <see cref="SetUp"/> or <see cref="SetUpAsync"/> is invoked.
        /// </summary>
        /// <param name="setUpFunc">The set up function. The first argument is the current count of invocations, and the second is the optional data object. The return value indicates success.</param>
        public void RegisterSetUp(Func<int, object?, CancellationToken, Task<bool>>? setUpFunc)
        {
            _setUpSet = true;
            _setUpFunc = setUpFunc;
            _autoSetUpFunc = null;
            _setUpCount = 0;
        }

        /// <summary>
        /// Registers the <paramref name="autoSetUpFunc"/> that will be executed whenever <see cref="SetUp"/> or <see cref="SetUpAsync"/> is invoked.
        /// </summary>
        /// <param name="autoSetUpFunc">The set up function. The first argument is the current count of invocations, and the second is the optional data object. The return value indicates success with a corresponding output to be logged.</param>
        /// <remarks>This will automatically perform a <see cref="TestFrameworkImplementor.AssertFail(string?)"/> on error. On success the output will be cache temporarily and logged within the logging-phase of the next executing test.</remarks>
        public void RegisterAutoSetUp(Func<int, object?, CancellationToken, Task<(bool, string?)>>? autoSetUpFunc)
        {
            _setUpSet = true;
            _setUpFunc = null;
            _autoSetUpFunc = autoSetUpFunc;
            _setUpCount = 0;
        }

        /// <summary>
        /// Executes the registered set up synchronsously.
        /// </summary>
        /// <param name="data">The optional data.</param>
        /// <returns><c>true</c> indicates that the set up was successful; otherwise, <c>false</c>.</returns>
        /// <remarks>This operation is thread-safe.</remarks>
        public bool SetUp(object? data = null) => SetUpAsync(data).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the registered set up asynchronsously.
        /// </summary>
        /// <param name="data">The optional data.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns><c>true</c> indicates that the set up was successful; otherwise, <c>false</c>.</returns>
        /// <remarks>This operation is thread-safe.</remarks>
        public async Task<bool> SetUpAsync(object? data = null, CancellationToken cancellationToken = default)
        {
            if (SetUpFunc == null && AutoSetUpFunc == null)
                throw new InvalidOperationException("Set up can not be invoked as no set up function has been registered; please use RegisterSetUp() ot AutoRegisterSetUp() to enable.");

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (SetUpFunc != null)
                    return await SetUpFunc(GetSetUpCount()++, data, cancellationToken).ConfigureAwait(false);

                if (AutoSetUpFunc != null)
                {
                    var (Success, Output) = await AutoSetUpFunc(GetSetUpCount()++, data, cancellationToken).ConfigureAwait(false);
                    if (Success)
                    {
                        if (!string.IsNullOrEmpty(Output))
                            _autoSetUpOutputs.Enqueue(Output);

                        return true;
                    }
                    else
                        throw new TestSetUpException(Output);
                }

                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the set up function crawling up the cloned hierarchy.
        /// </summary>
        private Func<int, object?, CancellationToken, Task<bool>>? SetUpFunc => _setUpSet ? _setUpFunc : _clonedFrom?.SetUpFunc;

        /// <summary>
        /// Gets the auto set up function crawling up the cloned hierarchy.
        /// </summary>
        private Func<int, object?, CancellationToken, Task<(bool, string?)>>? AutoSetUpFunc => _setUpSet ? _autoSetUpFunc : _clonedFrom?.AutoSetUpFunc;

        /// <summary>
        /// Gets the count (by reference) crawling up the cloned hierarchy.
        /// </summary>
        private ref int GetSetUpCount()
        {
            if (_setUpSet || _clonedFrom == null)
                return ref _setUpCount;

            return ref _clonedFrom.GetSetUpCount();
        }
    }
}