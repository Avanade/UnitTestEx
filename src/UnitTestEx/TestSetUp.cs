// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using CoreEx.Wildcards;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Functions;
using UnitTestEx.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using CoreEx.Configuration;

namespace UnitTestEx
{
    /// <summary>
    /// Orchestrates the set up for testing and provides default test settings.
    /// </summary>
    public class TestSetUp
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private int _setUpCount;
        private Func<int, object?, CancellationToken, Task<bool>>? _setUpFunc;

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
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.unittest.json", optional: true);

            if (environmentVariablePrefix == null)
                cb.AddEnvironmentVariables();
            else
                cb.AddEnvironmentVariables(environmentVariablePrefix.EndsWith("_", StringComparison.InvariantCulture) ? environmentVariablePrefix : environmentVariablePrefix + "_");

            cb.AddCommandLine(Environment.GetCommandLineArgs());
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
        /// Gets or sets the default username.
        /// </summary>
        /// <remarks>Defaults to '<c>Anonymous</c>'.</remarks>
        public string DefaultUsername { get; set; } = "Anonymous";

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
        /// <remarks>By default <see cref="EventDataBase.Id"/>, <see cref="EventDataBase.CorrelationId"/>, <see cref="EventDataBase.Timestamp"/> and <see cref="EventDataBase.ETag"/> are ignored.</remarks>
        public List<string> ExpectedEventsMembersToIgnore { get; set; } = new() { nameof(EventDataBase.Id), nameof(EventDataBase.CorrelationId), nameof(EventDataBase.Timestamp), nameof(EventDataBase.ETag) };

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
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.Username"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequestMessage, string?, CancellationToken, Task>? OnBeforeHttpRequestMessageSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send for the <see cref="Functions.HttpTriggerTester{TFunction}"/>.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.Username"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequest, string?, CancellationToken, Task>? OnBeforeHttpRequestSendAsync { get; set; }

        /// <summary>
        /// Registers the <paramref name="setUpFunc"/> that will be executed whenever <see cref="SetUp"/> or <see cref="SetUpAsync"/> is invoked.
        /// </summary>
        /// <param name="setUpFunc">The set up function. The first argument is the current count of invocations, and second is the optional data object. The return value indicates success.</param>
        public void RegisterSetUp(Func<int, object?, CancellationToken, Task<bool>> setUpFunc) => _setUpFunc = setUpFunc;

        /// <summary>
        /// Executes the registered set up asynchronsously.
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
            if (_setUpFunc == null)
                throw new InvalidOperationException("Set up can not be invoked as no set up function has been registered; please use RegisterSetUp() to enable.");

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _setUpFunc(_setUpCount++, data, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}