// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Configuration;
using CoreEx.Events;
using CoreEx.Wildcards;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Functions;

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

        /// <summary>
        /// Gets or sets the default <see cref="TestSetUp"/>.
        /// </summary>
        public static TestSetUp Default { get; set; } = new TestSetUp();

        /// <summary>
        /// Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts; defaults to <c>true</c>.
        /// </summary>
        public static bool FunctionTesterIncludeUnitTestConfiguration { get; set; } = true;

        /// <summary>
        /// Indicates whether to include user secrets configuration when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts; defaults to <c>false</c>.
        /// </summary>
        public static bool FunctionTesterIncludeUserSecrets { get; set; }

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
                cb.AddEnvironmentVariables(environmentVariablePrefix.EndsWith("_", StringComparison.InvariantCulture) ? environmentVariablePrefix : environmentVariablePrefix + "_");

            cb.AddCommandLine(System.Environment.GetCommandLineArgs());
            return cb.Build();
        }

        /// <summary>
        /// Gets the <see cref="DefaultSettings"/> using the <see cref="GetConfiguration(string?)"/>.
        /// </summary>
        /// <param name="environmentVariablePrefix">The prefix that the environment variables must start with (will automatically add a trailing underscore where not supplied).</param>
        /// <returns>The <see cref="DefaultSettings"/>.</returns>
        /// <remarks>The is built outside of any host and therefore host specific configuration is not available.</remarks>
        public static DefaultSettings GetSettings(string? environmentVariablePrefix = null) => new(GetConfiguration(environmentVariablePrefix));

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
        /// Defines an <see cref="CoreEx.Entities.IETag.ETag"/> value that <i>should</i> result in a concurrency error.
        /// </summary>
        /// <remarks>Defaults to '<c>ZZZZZZZZZZZZ</c>'.</remarks>
        public string ConcurrencyErrorETag { get; set; } = "ZZZZZZZZZZZZ";

        /// <summary>
        /// Indicates whether the <b>ExpectedEvents</b> functionality is enabled via the <see cref="UnitTestEx.Expectations"/> namespace.
        /// </summary>
        /// <remarks>Where enabled the <see cref="IEventSender"/> will be automatically replaced by the <see cref="Expectations.ExpectedEventPublisher"/> that is used by the <see cref="Expectations.EventExpectations.Assert"/> to verify that the
        /// expected events were sent. Therefore, the events will <b>not</b> be sent to any external eventing/messaging system as a result.</remarks>
        public bool ExpectedEventsEnabled { get; set; } = false;

        /// <summary>
        /// Indicates whether to verify that no events are published as the default behaviour. Defaults to <c>true</c>.
        /// </summary>
        /// <remarks>This is dependent on <see cref="ExpectedEventsEnabled"/>, please read for more information.</remarks>
        public bool ExpectNoEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets the <see cref="Expectations.EventExpectations.Expect(string?, EventData, string[])"/> and <see cref="Expectations.EventExpectations.Expect(string?, string, EventData, string[])"/> members to ignore.
        /// </summary>
        /// <remarks>By default <see cref="EventDataBase.Id"/>, <see cref="EventDataBase.CorrelationId"/>, <see cref="EventDataBase.Timestamp"/>, <see cref="EventDataBase.ETag"/> and <see cref="EventDataBase.Key"/> are ignored.</remarks>
        public List<string> ExpectedEventsPathsToIgnore { get; set; } = new() { nameof(EventDataBase.Id), nameof(EventDataBase.CorrelationId), nameof(EventDataBase.Timestamp), nameof(EventDataBase.ETag), nameof(EventDataBase.Key) };

        /// <summary>
        /// Gets or sets the <see cref="Expectations.EventExpectations"/> <see cref="Wildcard"/> parser.
        /// </summary>
        public Wildcard ExpectedEventsWildcard { get; set; } = new Wildcard(WildcardSelection.MultiAll);

        /// <summary>
        /// Gets or sets the <see cref="Expectations.EventExpectations"/> <see cref="EventDataFormatter"/> to determine the <see cref="EventDataFormatter.SubjectSeparatorCharacter"/> and <see cref="EventDataFormatter.TypeSeparatorCharacter"/>.
        /// </summary>
        public EventDataFormatter ExpectedEventsEventDataFormatter { get; set; } = new EventDataFormatter();

        /// <summary>
        /// Gets or sets the <see cref="Action"/> that enables the <see cref="IServiceCollection"/> to be updated before each execution.
        /// </summary>
        public Action<IServiceCollection>? ConfigureServices { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="CoreEx.ExecutionContext"/> factory to create a new instance outside of the host.
        /// </summary>
        public Func<IServiceProvider, CoreEx.ExecutionContext> ExecutionContextFactory { get; set; } = _ => new CoreEx.ExecutionContext();

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send for the <see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.UserName"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequestMessage, string?, CancellationToken, Task>? OnBeforeHttpRequestMessageSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send for the <see cref="Functions.HttpTriggerTester{TFunction}"/>.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.UserName"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequest, string?, CancellationToken, Task>? OnBeforeHttpRequestSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the minimum <see cref="LogLevel"/> when configuring the underlying host (see <see cref="ILoggingBuilder"/>).
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

        /// <inheritdoc/>
        /// <remarks>The <see cref="RegisterSetUp"/> and <see cref="RegisterAutoSetUp"/> will reference the originating unless explicitly registered (overridden) for the cloned instance.</remarks>
        public object Clone() => new TestSetUp
        {
            DefaultUserName = DefaultUserName,
            UserNameConverter = UserNameConverter,
            ConcurrencyErrorETag = ConcurrencyErrorETag,
            ExpectedEventsEnabled = ExpectedEventsEnabled,
            ExpectNoEvents = ExpectNoEvents,
            ExpectedEventsPathsToIgnore = ExpectedEventsPathsToIgnore,
            ExpectedEventsWildcard = ExpectedEventsWildcard,
            ExpectedEventsEventDataFormatter = ExpectedEventsEventDataFormatter,
            ConfigureServices = ConfigureServices,
            ExecutionContextFactory = ExecutionContextFactory,
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