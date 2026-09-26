// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using System;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Provides the .NET Aspire multi-host (distributed application) unit-testing capabilities bound to a specific AppHost <typeparamref name="TAppHost"/>, adding fluent-style
    /// method-chaining (<typeparamref name="TSelf"/>) over the host-agnostic <see cref="AspireTesterBase"/>.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
    /// <typeparam name="TSelf">The <see cref="AspireTesterBase{TAppHost, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    /// <remarks>Only fluent, method-chaining members (those that must return <typeparamref name="TSelf"/>) and the one build step that is genuinely bound to the concrete
    /// <typeparamref name="TAppHost"/> (<see cref="CreateBuilderAsync"/>) live here; everything else - including <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>,
    /// <see cref="AspireTesterBase.Http(string, string?)"/>, <see cref="AspireTesterBase.WaitForResourceAsync(string, TimeSpan?)"/> - lives on the non-generic <see cref="AspireTesterBase"/>
    /// so it remains reachable (e.g. via an extension method) without needing to know either type parameter.</remarks>
    public abstract class AspireTesterBase<TAppHost, TSelf> : AspireTesterBase
        where TAppHost : class
        where TSelf : AspireTesterBase<TAppHost, TSelf>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspireTesterBase{TAppHost, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected AspireTesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <inheritdoc/>
        protected override Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync() => DistributedApplicationTestingBuilder.CreateAsync<TAppHost>();

        /// <summary>
        /// Replaces the <see cref="TesterBaseCore.SetUp"/> by cloning the <paramref name="setUp"/>.
        /// </summary>
        /// <param name="setUp">The <see cref="TestSetUp"/></param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Updates the <see cref="TesterBaseCore.JsonSerializer"/> and <see cref="TesterBaseCore.JsonComparerOptions"/> from the <paramref name="setUp"/>.
        /// <para>Unlike <see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>'s (and other Tier 1 testers') equivalent, this does <b>not</b> <see cref="AspireTesterBase.OnResetHost">reset</see>
        /// the underlying <see cref="DistributedApplication"/>: <see cref="TestSetUp.ConfigureServices"/> - the reason Tier 1 must rebuild its single, in-process dependency injection (DI)
        /// container host - has no equivalent here, as the AppHost is a genuine, separately-built multi-process distributed application that <see cref="TestSetUp"/> does not configure. Where
        /// its properties are consumed by a Tier 2 test (e.g. <see cref="TestSetUp.OnBeforeHttpRequestMessageSendAsync"/> when sending a request via <see cref="AspireTesterBase.Http(string, string?)"/>,
        /// or <see cref="TesterBaseCore.JsonSerializer"/>/<see cref="TesterBaseCore.JsonComparerOptions"/> when asserting a response), they are read live at request/assert time, so no reset is
        /// required for a subsequent call to see the change.</para></remarks>
        public TSelf UseSetUp(TestSetUp setUp)
        {
            SetUp = setUp?.Clone() ?? throw new ArgumentNullException(nameof(setUp));
            JsonSerializer = SetUp.JsonSerializer;
            JsonComparerOptions = SetUp.JsonComparerOptions;
            return (TSelf)this;
        }

        /// <summary>
        /// Updates (replaces) the default test <see cref="TesterBaseCore.UserName"/>.
        /// </summary>
        /// <param name="userName">The test user name (a <c>null</c> value will reset to <see cref="TesterBaseCore.SetUp"/> <see cref="TestSetUp.DefaultUserName"/>).</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseUser(string? userName)
        {
            UserName = userName ?? SetUp.DefaultUserName;
            return (TSelf)this;
        }

        /// <summary>
        /// Updates (replaces) the default test <see cref="TesterBaseCore.UserName"/>.
        /// </summary>
        /// <param name="userIdentifier">The test user identifier (a <c>null</c> value will reset to <see cref="TesterBaseCore.SetUp"/> <see cref="TestSetUp.DefaultUserName"/>).</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="TestSetUp.UserNameConverter"/> is required for the conversion to take place.</remarks>
        public TSelf UseUser(object? userIdentifier)
        {
            if (userIdentifier == null)
                return UseUser(null);

            if (SetUp.UserNameConverter == null)
                throw new InvalidOperationException($"The {nameof(TestSetUp)}.{nameof(TestSetUp.UserNameConverter)} must be defined to support user identifier conversion.");

            return UseUser(SetUp.UserNameConverter(userIdentifier));
        }

        /// <summary>
        /// Updates the <see cref="TesterBaseCore.JsonSerializer"/> used by the <see cref="AspireTesterBase{TAppHost, TSelf}"/> itself, not any underlying resource which should be configured separately.
        /// </summary>
        /// <param name="jsonSerializer">The <see cref="Json.IJsonSerializer"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseJsonSerializer(Json.IJsonSerializer jsonSerializer)
        {
            JsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            return (TSelf)this;
        }

        /// <summary>
        /// Updates the <see cref="TesterBaseCore.JsonComparerOptions"/> used by the <see cref="AspireTesterBase{TAppHost, TSelf}"/> itself, not any underlying resource which should be configured separately.
        /// </summary>
        /// <param name="options">The <see cref="Json.JsonElementComparerOptions"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Where the <see cref="Json.JsonElementComparerOptions.JsonSerializer"/> is <c>null</c> then the <see cref="TesterBaseCore.JsonSerializer"/> will be used.</remarks>
        public TSelf UseJsonComparerOptions(Json.JsonElementComparerOptions options)
        {
            JsonComparerOptions = options ?? throw new ArgumentNullException(nameof(options));
            return (TSelf)this;
        }

        /// <summary>
        /// Overrides an environment variable for the named resource before the underlying <see cref="DistributedApplication"/> is built.
        /// </summary>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="key">The environment variable name.</param>
        /// <param name="value">The environment variable value.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This is the only override surface available for a Tier 2 tester as each resource runs in its own, separate process with no shared, in-process dependency injection
        /// (DI) container to reach into; must be called before the underlying <see cref="DistributedApplication"/> has been built (see <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>).</remarks>
        public TSelf WithResourceEnvironment(string resourceName, string key, string value)
        {
            if (resourceName is null) throw new ArgumentNullException(nameof(resourceName));
            if (key is null) throw new ArgumentNullException(nameof(key));

            lock (SyncRoot)
            {
                if (IsDistributedApplicationBuilding)
                    throw new InvalidOperationException($"{nameof(WithResourceEnvironment)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                ConfigureBuilderActions.Add(builder => builder.CreateResourceBuilder<IResourceWithEnvironment>(resourceName).WithEnvironment(key, value));
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Opts back into the AppHost's own raw logging (its start-up banner, DCP process management, and each resource's own console output) being written to the test output, in addition
        /// to what UnitTestEx itself reports via <see cref="Reason"/>/<see cref="Delay(TimeSpan?, string?)"/>/the <see cref="AspireTesterBase.Http(string, string?)"/> request-scoped "LOGGING &gt;" section.
        /// </summary>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>By default this raw firehose is suppressed as it otherwise duplicates what UnitTestEx already surfaces cleanly (and correlated); enable it when troubleshooting an
        /// AppHost/resource start-up failure that needs the full, unfiltered diagnostic output. Must be called before the underlying <see cref="DistributedApplication"/> has been built
        /// (see <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>).</remarks>
        public TSelf EnableHostDiagnostics()
        {
            lock (SyncRoot)
            {
                if (IsDistributedApplicationBuilding)
                    throw new InvalidOperationException($"{nameof(EnableHostDiagnostics)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                HostDiagnosticsEnabled = true;
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Writes the specified <paramref name="reason"/> to the test output to provide additional context (e.g. why a particular action, or wait, is being performed).
        /// </summary>
        /// <param name="reason">The reason text.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf Reason(string reason)
        {
            WriteReason(reason);
            return (TSelf)this;
        }

        /// <summary>
        /// Delays for the specified <paramref name="duration"/>, then writes any resource log messages captured (across <i>all</i> resources) during that time to the test output.
        /// </summary>
        /// <param name="duration">The duration to delay; defaults to <see cref="TesterBaseCore.DefaultDelayDuration"/> where not specified.</param>
        /// <param name="reason">The reason for delaying (written to the test output for context); defaults to "No reason specified" where not specified.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Useful when waiting on background/inter-resource activity (e.g. a message being processed by a downstream resource) that is not tied to a specific HTTP request/response (see
        /// <see cref="AspireTesterBase.Http(string, string?)"/> for the latter, request-scoped, correlation).</remarks>
        public TSelf Delay(TimeSpan? duration = null, string? reason = null) => WriteDelay(reason, duration).ContinueWith(_ => (TSelf)this).Result;

        /// <summary>
        /// Delays for the specified <paramref name="durationInMilliseconds"/>, then writes any resource log messages captured (across <i>all</i> resources) during that time to the test output.
        /// </summary>
        /// <param name="durationInMilliseconds">The amount of time, in milliseconds, to delay. Must be a non-negative <see cref="int"/>.</param>
        /// <param name="reason">The reason for delaying (written to the test output for context); defaults to "No reason specified" where not specified.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Useful when waiting on background/inter-resource activity (e.g. a message being processed by a downstream resource) that is not tied to a specific HTTP request/response (see
        /// <see cref="AspireTesterBase.Http(string, string?)"/> for the latter, request-scoped, correlation).</remarks>
        public TSelf Delay(int durationInMilliseconds, string? reason = null) => Delay(TimeSpan.FromMilliseconds(durationInMilliseconds), reason);
    }
}
