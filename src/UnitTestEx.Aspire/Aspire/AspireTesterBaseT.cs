// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
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
        /// Registers a callback to invoke once the underlying <see cref="DistributedApplication"/> has been built but before it is started - i.e. before any resource (project, container,
        /// executable, etc.) begins running.
        /// </summary>
        /// <param name="beforeStart">The callback, given the built (not yet started) <see cref="DistributedApplication"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Useful to prepare an external dependency whose connection details are only resolvable via the AppHost (e.g. a connection-string resource added via the AppHost's own
        /// <c>AddConnectionString</c>) - such as running database migrations/seeding, clearing a cache, or resetting a messaging topic/queue - <i>before</i> any project resource that
        /// depends on it starts running and potentially races against that very same setup (e.g. connecting to a not-yet-migrated database). Registered callbacks run, in order, after
        /// <c>BuildAsync</c> and before <c>StartAsync</c>; an exception thrown by any callback aborts start-up (the partially-built <see cref="DistributedApplication"/> is disposed and the
        /// exception propagates), consistent with a resource failing to start.
        /// <para><i>Important:</i> the callback must operate directly against the <see cref="DistributedApplication"/> passed to it - it must <b>not</b> call back into
        /// <see cref="AspireTesterBase.GetDistributedApplicationAsync"/> (or any instance method that does, e.g. the instance <see cref="AspireTesterBase.WaitForResourceAsync(string, TimeSpan?)"/>):
        /// that method's own construction is still in-flight at this point, so awaiting it from within the callback that is part of that very construction will deadlock/hang rather than
        /// reuse it. It also must <b>not</b> call Aspire's own <c>DistributedApplication.GetConnectionStringAsync</c>/<c>GetEndpoint</c>/<c>CreateHttpClient</c> testing extensions
        /// (<c>Aspire.Hosting.Testing</c>) - these throw <see cref="InvalidOperationException"/> at this point as they require the application to have already started; use
        /// <see cref="AspireTesterBase.GetConnectionStringAsync(DistributedApplication, string, CancellationToken)"/> instead to resolve a connection string.</para>
        /// <para>Must be called before the underlying <see cref="DistributedApplication"/> has been built (see <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>).</para></remarks>
        public TSelf BeforeStart(Func<DistributedApplication, Task> beforeStart)
        {
            if (beforeStart is null) throw new ArgumentNullException(nameof(beforeStart));

            lock (SyncRoot)
            {
                if (IsDistributedApplicationBuilding)
                    throw new InvalidOperationException($"{nameof(BeforeStart)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                BeforeStartActions.Add(beforeStart);
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Registers a callback to invoke once the underlying <see cref="DistributedApplication"/> has been started - i.e. after every resource (project, container, executable, etc.)
        /// has been kicked off.
        /// </summary>
        /// <param name="afterStart">The callback, given the started <see cref="DistributedApplication"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The natural place to wait for one or more resources to become healthy (e.g. via the
        /// <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/>/<see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string[], TimeSpan?)"/>
        /// extension methods) - or perform any other post-start-up set-up dependent on every resource already running - before a test's own set-up (e.g. <c>OneTimeSetUp</c>) proceeds to use them.
        /// Registered callbacks run, in order, immediately after <c>StartAsync</c> succeeds; an exception thrown by any callback aborts start-up (the started <see cref="DistributedApplication"/>
        /// is disposed and the exception propagates), consistent with <see cref="BeforeStart"/>'s behaviour.
        /// <para><i>Important:</i> the callback must operate directly against the <see cref="DistributedApplication"/> passed to it - it must <b>not</b> call back into
        /// <see cref="AspireTesterBase.GetDistributedApplicationAsync"/> (or any instance method that does, e.g. the instance <see cref="AspireTesterBase.WaitForResourceAsync(string, TimeSpan?)"/>):
        /// that method's own construction is still in-flight at this point, so awaiting it from within the callback that is part of that very construction will deadlock/hang rather than
        /// reuse it; use the <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/> extension method (which operates directly against the
        /// callback's own <see cref="DistributedApplication"/> parameter) instead.</para>
        /// <para>Must be called before the underlying <see cref="DistributedApplication"/> has been built (see <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>).</para></remarks>
        public TSelf AfterStart(Func<DistributedApplication, Task> afterStart)
        {
            if (afterStart is null) throw new ArgumentNullException(nameof(afterStart));

            lock (SyncRoot)
            {
                if (IsDistributedApplicationBuilding)
                    throw new InvalidOperationException($"{nameof(AfterStart)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                AfterStartActions.Add(afterStart);
            }

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
        /// to what UnitTestEx itself reports via <see cref="Checkpoint"/>/<see cref="Delay(TimeSpan?, string?)"/>/the <see cref="AspireTesterBase.Http(string, string?)"/> request-scoped "LOGGING &gt;" section.
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
        /// Registers a continuous, streaming check that immediately fails the test the moment any resource logs an entry at or above <paramref name="minimumLevel"/> - checked every time
        /// resource log activity is drained, i.e. by every <see cref="Checkpoint"/>, <see cref="Delay(TimeSpan?, string?)"/> and <see cref="AspireTesterBase.Http(string, string?)"/> call from
        /// this point forward (not a single, one-shot, end-of-test scan).
        /// </summary>
        /// <param name="minimumLevel">The minimum <see cref="LogLevel"/> that triggers a failure; defaults to <see cref="LogLevel.Error"/> - which is also the effective default even where
        /// this method is never called at all (see <see cref="AspireTesterBase.ErrorWhenLogContainsMinimumLevel"/>). Pass <see cref="LogLevel.None"/> to opt out entirely.</param>
        /// <param name="exclude">Zero or more <c>*</c>/<c>?</c> wildcard, case-insensitive patterns (matched as a "contains" against the entry's fully formatted text) for known/expected noise
        /// that should not trigger a failure despite otherwise qualifying; a match here always wins, even where <paramref name="include"/> also matches.</param>
        /// <param name="include">Zero or more <c>*</c>/<c>?</c> wildcard, case-insensitive patterns that narrow (rather than widen) what is checked - where specified, an otherwise-qualifying
        /// entry must match at least one of these to trigger a failure. Leave <c>null</c>/empty (the default) to check every qualifying entry regardless of its text.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Enabled by default at <see cref="LogLevel.Error"/> without needing to call this at all; calling it is only required to change the <paramref name="minimumLevel"/>, add
        /// <paramref name="exclude"/>/<paramref name="include"/> patterns, or opt out entirely (via <see cref="LogLevel.None"/>). Can be called at any time (unlike e.g.
        /// <see cref="WithResourceEnvironment"/>/<see cref="EnableHostDiagnostics"/>, this has no bearing on how the underlying <see cref="DistributedApplication"/> is built, so there is no
        /// "must be called before start" restriction); each call fully <i>replaces</i> the prior configuration (all three parameters) rather than merging with it - pass the complete desired
        /// state each time.
        /// <para>Only a resource log entry that is subsequently <i>drained</i> (via <see cref="Checkpoint"/>/<see cref="Delay(TimeSpan?, string?)"/>/<see cref="AspireTesterBase.Http(string, string?)"/>)
        /// is checked; anything still buffered (or still pending completion by its own terminating header line) when the test ends is never seen unless a final <see cref="Checkpoint"/> call is
        /// made - so a trailing <c>Test.Checkpoint("Final log check.")</c> is recommended at the end of a test that relies on this to also catch background/hosted-service-only activity that no
        /// other call happens to drain.</para></remarks>
        public TSelf ErrorWhenLogContains(LogLevel minimumLevel = LogLevel.Error, string[]? exclude = null, string[]? include = null)
        {
            lock (SyncRoot)
            {
                ErrorWhenLogContainsMinimumLevel = minimumLevel;
                ErrorWhenLogContainsExcludePatterns = exclude ?? [];
                ErrorWhenLogContainsIncludePatterns = include ?? [];
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Writes the specified <paramref name="reason"/> to the test output to provide additional context (e.g. why a particular action is being performed), then immediately writes any
        /// resource log messages captured (across <i>all</i> resources) since the last checkpoint/delay to the test output - i.e. a zero-wait equivalent of <see cref="Delay(TimeSpan?, string?)"/>.
        /// </summary>
        /// <param name="reason">The reason text.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf Checkpoint(string reason)
        {
            WriteCheckpoint(reason);
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

        /// <summary>
        /// Asserts that at least one resource log entry captured so far - across every resource, or only <paramref name="resourceName"/> where specified - contains the wildcard
        /// (<c>*</c>/<c>?</c>) <paramref name="pattern"/>.
        /// </summary>
        /// <param name="pattern">The wildcard (<c>*</c>/<c>?</c>), case-insensitive "contains" pattern that at least one log entry's fully formatted text must match.</param>
        /// <param name="resourceName">The optional resource name (as configured within the AppHost) to scope the check to; where not specified, every resource is checked.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>An immediate, one-shot check against every resource log entry captured for the lifetime of the test so far - whether already drained/reported via <see cref="Checkpoint"/>/
        /// <see cref="Delay(TimeSpan?, string?)"/>/<see cref="AspireTesterBase.Http(string, string?)"/> or not - unlike <see cref="ErrorWhenLogContains"/>'s continuous, drain-only streaming
        /// check. Call it whenever (and as many times as) needed, typically right after whatever action is expected to have produced the entry - there is no "was this seen by the end of the
        /// test" ambiguity to reason about.</remarks>
        public TSelf AssertLogContains(string pattern, string? resourceName = null)
        {
            AssertResourceLogContains(pattern, resourceName);
            return (TSelf)this;
        }

        /// <summary>
        /// Asserts that <b>no</b> resource log entry captured so far - across every resource, or only <paramref name="resourceName"/> where specified - contains the wildcard
        /// (<c>*</c>/<c>?</c>) <paramref name="pattern"/>.
        /// </summary>
        /// <param name="pattern">The wildcard (<c>*</c>/<c>?</c>), case-insensitive "contains" pattern that no log entry's fully formatted text may match.</param>
        /// <param name="resourceName">The optional resource name (as configured within the AppHost) to scope the check to; where not specified, every resource is checked.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>See <see cref="AssertLogContains(string, string?)"/> remarks - the same immediate, one-shot semantics apply here (inverted).</remarks>
        public TSelf AssertLogNotContains(string pattern, string? resourceName = null)
        {
            AssertResourceLogNotContains(pattern, resourceName);
            return (TSelf)this;
        }

        /// <summary>
        /// Discards every resource log entry captured so far, across all resources, along with the per-resource watermarks used by <see cref="Checkpoint"/>/<see cref="Delay(TimeSpan?, string?)"/>
        /// - i.e. puts resource log capture back into the same state as immediately after start-up.
        /// </summary>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Only relevant where the underlying <see cref="DistributedApplication"/> host is deliberately reused across multiple tests (e.g. via a shared fixture, to
        /// avoid repeatedly paying its start-up cost) - otherwise call this explicitly, typically as the first line of such a test, so <see cref="ErrorWhenLogContains"/>/
        /// <see cref="AssertLogContains(string, string?)"/>/<see cref="AssertLogNotContains(string, string?)"/> only ever see that test's own activity. Does not reset any
        /// <see cref="ErrorWhenLogContains"/> configuration - only previously captured log data.</remarks>
        public TSelf ResetLogs()
        {
            ResetResourceLogs();
            return (TSelf)this;
        }
    }
}
