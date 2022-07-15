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
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send for the <see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.Username"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequestMessage, string?, CancellationToken, Task>? OnBeforeHttpRequestMessageSendAsync { get; set; }

        /// <summary>
        /// Gets or sets the function that enables the <see cref="HttpRequestMessage"/> to be updated before each send for the <see cref="Functions.HttpTriggerTester{TFunction}"/>.
        /// </summary>
        /// <remarks>The second parameter (<see cref="string"/>) is set to the <see cref="TesterBase.Username"/>. This provides an opportunity where needed to add the likes of <see href="https://oauth.net/2/access-tokens/">OAuth tokens</see>, etc.</remarks>
        public Func<HttpRequest, string?, CancellationToken, Task>? OnBeforeHttpRequestSendAsync { get; set; }
    }
}