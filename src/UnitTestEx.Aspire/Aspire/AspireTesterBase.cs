// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Provides the non-generic, host-agnostic base for the .NET Aspire multi-host (distributed application) unit-testing capabilities - the Aspire-specific equivalent of
    /// <see cref="TesterBase"/> (its single-host, dependency injection (DI) counterpart).
    /// </summary>
    /// <remarks>This extends the host-agnostic <see cref="TesterBaseCore"/> directly (<i>not</i> <see cref="TesterBase"/>) as the underlying <see cref="DistributedApplication"/> is a real,
    /// multi-process distributed application with no single, in-process dependency injection (DI) container to reach into.
    /// <para>Exists primarily so an extension method (or any other code that does not know/care about the concrete AppHost or <c>TSelf</c> type parameters -
    /// see <see cref="AspireTesterBase{TAppHost, TSelf}"/>) can still reach the capabilities that do not depend on either - e.g. <see cref="GetDistributedApplicationAsync"/>,
    /// <see cref="Http(string, string?)"/>, <see cref="WaitForResourceAsync(string, TimeSpan?)"/>. Fluent, method-chaining configuration members that must return <c>TSelf</c>
    /// (e.g. <see cref="AspireTesterBase{TAppHost, TSelf}.UseSetUp(TestSetUp)"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.WithResourceEnvironment(string, string, string)"/>) necessarily
    /// remain on <see cref="AspireTesterBase{TAppHost, TSelf}"/> only.</para></remarks>
    public abstract class AspireTesterBase : TesterBaseCore, IHttpClientSource, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ResourceLogEntry>> _resourceLogBuffers = new();
        private readonly ConcurrentDictionary<string, int> _elapsedLogLineCounts = new();
        private ResourceLogCaptureProvider? _resourceLogCaptureProvider;
        private Task<DistributedApplication>? _appTask;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the minimum <see cref="LogLevel"/> that triggers an immediate test failure when a resource log entry at (or above) it is subsequently drained - see
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/>.
        /// </summary>
        /// <remarks>Enabled by default at <see cref="LogLevel.Error"/> - this is a brand-new capability with no existing tests relying on it being off, and catching an unexpected resource
        /// error is almost always preferable to silently letting it slide. To opt out entirely, call <see cref="AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/>
        /// with <see cref="LogLevel.None"/> (which, being numerically above <see cref="LogLevel.Critical"/>, no resource log entry can ever meet or exceed).</remarks>
        protected LogLevel ErrorWhenLogContainsMinimumLevel { get; set; } = LogLevel.Error;

        /// <summary>
        /// Gets or sets the wildcard (<c>*</c>/<c>?</c>) exclude patterns checked (case-insensitively, as a "contains" match) against an otherwise-violating resource log entry's text - a
        /// match here always suppresses the entry, even where it also matches <see cref="ErrorWhenLogContainsIncludePatterns"/> - see
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/>.
        /// </summary>
        protected IReadOnlyList<string> ErrorWhenLogContainsExcludePatterns { get; set; } = [];

        /// <summary>
        /// Gets or sets the wildcard (<c>*</c>/<c>?</c>) include patterns that an otherwise-qualifying resource log entry's text must match at least one of (case-insensitively, as a
        /// "contains" match) to be reported - see <see cref="AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/>. An empty list (the default) means
        /// every entry that meets <see cref="ErrorWhenLogContainsMinimumLevel"/> qualifies; i.e. this narrows rather than widens what is checked.
        /// </summary>
        protected IReadOnlyList<string> ErrorWhenLogContainsIncludePatterns { get; set; } = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireTesterBase"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected AspireTesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <summary>
        /// Gets the queued builder-configuration actions applied, in order, immediately before the underlying <see cref="DistributedApplication"/> is built.
        /// </summary>
        /// <remarks>Populated by <see cref="AspireTesterBase{TAppHost, TSelf}.WithResourceEnvironment(string, string, string)"/>.</remarks>
        protected List<Action<IDistributedApplicationTestingBuilder>> ConfigureBuilderActions { get; } = [];

        /// <summary>
        /// Gets the queued callbacks invoked, in order, once the underlying <see cref="DistributedApplication"/> has been built but before it is started - i.e. before any resource
        /// (project, container, executable, etc.) begins running.
        /// </summary>
        /// <remarks>Populated by <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart(Func{DistributedApplication, Task})"/>. Useful to prepare an external dependency whose connection
        /// details are only resolvable via the AppHost (e.g. a connection-string resource added via the AppHost's own <c>AddConnectionString</c>) - such as running database
        /// migrations/seeding, clearing a cache, or resetting a messaging topic/queue to a known state - <i>before</i> any project resource that depends on it starts running and
        /// potentially races against that very same setup (e.g. connecting to a not-yet-migrated database).
        /// <para><i>Important:</i> a callback must operate directly against the <see cref="DistributedApplication"/> passed to it - it must <b>not</b> call back into this tester's own
        /// <see cref="GetDistributedApplicationAsync"/> (or any extension method that does, e.g. a <c>MigrateXxxAsync</c> helper written against the tester): that method's own construction
        /// is still in-flight at this point, so awaiting it from within the callback that is part of that very construction will deadlock/hang rather than reuse it. It also must <b>not</b>
        /// call Aspire's own <c>DistributedApplication.GetConnectionStringAsync</c>/<c>GetEndpoint</c>/<c>CreateHttpClient</c> testing extensions (<c>Aspire.Hosting.Testing</c>) - these
        /// throw <see cref="InvalidOperationException"/> at this point as they require the application to have already started; use <see cref="GetConnectionStringAsync"/> instead.</para></remarks>
        protected List<Func<DistributedApplication, Task>> BeforeStartActions { get; } = [];

        /// <summary>
        /// Gets the queued callbacks invoked, in order, once the underlying <see cref="DistributedApplication"/> has been started - i.e. after every resource (project, container,
        /// executable, etc.) has been kicked off (though not necessarily healthy/ready yet - see <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/>).
        /// </summary>
        /// <remarks>Populated by <see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart(Func{DistributedApplication, Task})"/>. This is the natural place to wait for one or more resources
        /// to become healthy (e.g. via the <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/>/<see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string[], TimeSpan?)"/>
        /// extension methods) before a test's own set-up (e.g. <c>OneTimeSetUp</c>) proceeds to use them.
        /// <para><i>Important:</i> a callback must operate directly against the <see cref="DistributedApplication"/> passed to it - it must <b>not</b> call back into this tester's own
        /// <see cref="GetDistributedApplicationAsync"/> (or any instance method that does, e.g. the instance <see cref="WaitForResourceAsync(string, TimeSpan?)"/>): that method's own
        /// construction is still in-flight at this point, so awaiting it from within the callback that is part of that very construction will deadlock/hang rather than reuse it; use the
        /// <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/> extension method (which operates directly against the passed-in
        /// <see cref="DistributedApplication"/>) instead.</para></remarks>
        protected List<Func<DistributedApplication, Task>> AfterStartActions { get; } = [];

        /// <summary>
        /// Resolves the connection string for the named resource directly against the <see cref="DistributedApplication"/>'s resource model.
        /// </summary>
        /// <param name="app">The <see cref="DistributedApplication"/>.</param>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The connection string, or <c>null</c> if the resource has none configured.</returns>
        /// <exception cref="ArgumentException">The resource was not found, or does not expose a connection string.</exception>
        /// <remarks>Unlike Aspire's own <c>DistributedApplication.GetConnectionStringAsync(resourceName)</c> testing extension (<c>Aspire.Hosting.Testing</c>) - which throws
        /// <see cref="InvalidOperationException"/> unless the application has already started (i.e. its underlying <c>IHostApplicationLifetime.ApplicationStarted</c> has fired) - this
        /// resolves the resource directly via <see cref="DistributedApplicationModel"/>, so it is safe to call from a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/> callback
        /// (i.e. before <c>StartAsync</c> has even been invoked), as well as at any other time.
        /// <para>This only works for resources whose connection string does not depend on a dynamically-allocated endpoint - e.g. a static, externally-hosted dependency added via the
        /// AppHost's own <c>AddConnectionString(name)</c> (the primary <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/> use case), whose value is resolved directly from
        /// configuration/parameters. For a resource whose connection string is only known once the resource itself has actually started and allocated an endpoint (e.g. a container
        /// resource), calling this before the application has started will return an incomplete/unresolved value; use Aspire's own <c>GetConnectionStringAsync</c> after the application
        /// has started for those instead.</para></remarks>
        public static async Task<string?> GetConnectionStringAsync(DistributedApplication app, string resourceName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentException.ThrowIfNullOrEmpty(resourceName);

            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            if (!model.Resources.TryGetByName(resourceName, out var resource))
                throw new ArgumentException($"Resource '{resourceName}' not found.", nameof(resourceName));

            if (resource is not IResourceWithConnectionString resourceWithConnectionString)
                throw new ArgumentException($"Resource '{resourceName}' does not expose a connection string.", nameof(resourceName));

            return await resourceWithConnectionString.GetConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a value indicating whether the underlying <see cref="DistributedApplication"/> has already started being built (see <see cref="GetDistributedApplicationAsync"/>).
        /// </summary>
        /// <remarks>Used to guard configuration that must be applied before the underlying <see cref="DistributedApplication"/> is built (see
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.WithResourceEnvironment(string, string, string)"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.EnableHostDiagnostics"/>).</remarks>
        protected bool IsDistributedApplicationBuilding => _appTask is not null;

        /// <summary>
        /// Gets the underlying <see cref="DistributedApplication"/>; builds and starts on first access.
        /// </summary>
        /// <returns>The started <see cref="DistributedApplication"/>.</returns>
        /// <remarks>This is the escape hatch for anything not already surfaced by <see cref="AspireTesterBase"/> itself - e.g. resolving a resource's connection string via
        /// Aspire's own <c>GetConnectionStringAsync(resourceName)</c>, inspecting <see cref="DistributedApplication.ResourceNotifications"/> directly, or reaching into
        /// <see cref="DistributedApplication.Services"/> for the AppHost's own (not a resource's) dependency injection (DI) container.
        /// <para><i>Important:</i> do <b>not</b> call <c>DisposeAsync()</c>/<c>StopAsync()</c> on the returned instance directly - this bypasses UnitTestEx's own lifecycle tracking (leaving it
        /// believing the host is still instantiated when it is not) and will surface as confusing failures on a subsequent call. Use <see cref="TesterBaseCore.ResetHost"/> instead to tear down
        /// and force a rebuild on next access.</para></remarks>
        public Task<DistributedApplication> GetDistributedApplicationAsync()
        {
            lock (SyncRoot)
            {
                return _appTask ??= CreateDistributedApplicationAsync();
            }
        }

        /// <summary>
        /// Creates the <see cref="IDistributedApplicationTestingBuilder"/> for the concrete AppHost.
        /// </summary>
        /// <returns>The <see cref="IDistributedApplicationTestingBuilder"/>.</returns>
        /// <remarks>The only step of building the underlying <see cref="DistributedApplication"/> that depends on the concrete AppHost <see cref="Type"/> - a generic type parameter only
        /// available on <see cref="AspireTesterBase{TAppHost, TSelf}"/>; every other build/start-up step is host-agnostic and lives here (see <see cref="GetDistributedApplicationAsync"/>).</remarks>
        protected abstract Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync();

        /// <summary>
        /// Creates, builds and starts the underlying <see cref="DistributedApplication"/>.
        /// </summary>
        private async Task<DistributedApplication> CreateDistributedApplicationAsync()
        {
            var builder = await CreateBuilderAsync().ConfigureAwait(false);

            if (!HostDiagnosticsEnabled)
                // By default the AppHost's own logging (its start-up banner, DCP process management, and - critically - each resource's own console output mirrored under a
                // "{ApplicationName}.Resources.{resourceName}" category; see 'EnableResourceLogging') writes straight to the test's Standard Output via its default provider(s) (e.g. console),
                // duplicating what UnitTestEx already captures and reports cleanly (and correlated) via Checkpoint()/Delay()/the Http() request-scoped "LOGGING >" section. Remove the AppHost's
                // own provider(s) so only what UnitTestEx explicitly writes reaches the test output; call EnableHostDiagnostics() before this point to opt back into the raw firehose (e.g. when
                // troubleshooting an AppHost/resource start-up failure).
                builder.Services.RemoveAll<ILoggerProvider>();

            builder.Services.Configure<LoggerFilterOptions>(options =>
                // Exempt our own capture provider from any ambient filtering (by provider, not category) so it always sees everything, regardless of the AppHost's own console noise level
                // (which the AppHost's own default logging configuration governs independently and is not something UnitTestEx overrides).
                options.Rules.Add(new LoggerFilterRule(typeof(ResourceLogCaptureProvider).FullName, null, LogLevel.Trace, null)));

            builder.Services.AddSingleton<ILoggerProvider>(_resourceLogCaptureProvider = new ResourceLogCaptureProvider($"{builder.Environment.ApplicationName}.Resources.", _resourceLogBuffers));

            foreach (var configure in ConfigureBuilderActions)
            {
                configure(builder);
            }

            OnHostStartUp();

            var app = await builder.BuildAsync().ConfigureAwait(false);

            try
            {
                // Give any queued BeforeStartActions (see AspireTesterBase{TAppHost, TSelf}.BeforeStart) the chance to prepare an external dependency (e.g. migrate/seed a database resolved
                // via app.GetConnectionStringAsync) before any resource is actually started/kicked off below, so it cannot race against that very same setup.
                foreach (var beforeStart in BeforeStartActions)
                {
                    await beforeStart(app).ConfigureAwait(false);
                }

                await app.StartAsync().ConfigureAwait(false);

                // Give any queued AfterStartActions (see AspireTesterBase{TAppHost, TSelf}.AfterStart) the chance to act now every resource has been kicked off (e.g. waiting for one or
                // more to become healthy via the WaitForResourceAsync extension method) before the tester itself is handed back to the caller.
                foreach (var afterStart in AfterStartActions)
                {
                    await afterStart(app).ConfigureAwait(false);
                }
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return app;
        }

        /// <inheritdoc/>
        /// <remarks>Sealed as the Aspire-specific extensibility point for logic that must run around start-up is the <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/> fluent pair - unlike this virtual hook (inherited from <see cref="TesterBaseCore"/>), both receive the actual
        /// <see cref="DistributedApplication"/> instance and run at a precisely-defined point (immediately before/after <c>StartAsync</c>); prefer those instead.</remarks>
        protected sealed override void OnHostStartUp() => base.OnHostStartUp();

        /// <inheritdoc/>
        protected override void OnResetHost()
        {
            Task<DistributedApplication>? appTask;
            lock (SyncRoot)
            {
                appTask = _appTask;
                _appTask = null;
            }

            if (appTask is { IsCompletedSuccessfully: true })
            {
                appTask.Result.Dispose();
                Implementor.WriteLine("");
                Implementor.WriteLine("** The underlying UnitTestEx 'AspireTester' distributed application host has been reset. **");
                Implementor.WriteLine("");
            }
        }

        /// <summary>
        /// Gets a value indicating whether the AppHost's own raw logging (its start-up banner, DCP process management, and each resource's own console output mirrored via
        /// 'EnableResourceLogging') is written to the test output, in addition to what UnitTestEx itself reports (see <see cref="AspireTesterBase{TAppHost, TSelf}.EnableHostDiagnostics"/>).
        /// </summary>
        public bool HostDiagnosticsEnabled { get; protected set; }

        /// <summary>
        /// Gets the default timeout used by <see cref="WaitForResourceAsync(string, TimeSpan?)"/> when none is specified.
        /// </summary>
        public static TimeSpan DefaultWaitForResourceTimeout { get; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Waits for the named resource to report a healthy status.
        /// </summary>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="timeout">The timeout (defaults to <see cref="DefaultWaitForResourceTimeout"/>); pass <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <remarks>A resource that never becomes healthy (e.g. a misconfigured health check) should fail the test fast rather than hang it indefinitely, hence the default timeout.
        /// <para><i>Important:</i> do <b>not</b> call this from within a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/>
        /// callback - it calls <see cref="GetDistributedApplicationAsync"/>, whose own construction is still in-flight at that point, so awaiting it will deadlock/hang; use the
        /// <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/> extension method (against the callback's own <see cref="DistributedApplication"/>
        /// parameter) instead.</para></remarks>
        public async Task WaitForResourceAsync(string resourceName, TimeSpan? timeout = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(resourceName);

            var app = await GetDistributedApplicationAsync().ConfigureAwait(false);
            await app.WaitForResourceAsync(resourceName, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for all of the named resources to report a healthy status, concurrently.
        /// </summary>
        /// <param name="resourceNames">The resource names (as configured within the AppHost).</param>
        /// <param name="timeout">The timeout (defaults to <see cref="DefaultWaitForResourceTimeout"/>) applied to each resource independently; pass
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <remarks>Equivalent to awaiting <see cref="WaitForResourceAsync(string, TimeSpan?)"/> for each resource in parallel; if any resource fails to become healthy within the
        /// <paramref name="timeout"/>, the resulting exception is propagated once all waits have completed (or faulted).
        /// <para><i>Important:</i> do <b>not</b> call this from within a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/>
        /// callback - see <see cref="WaitForResourceAsync(string, TimeSpan?)"/> remarks; use the
        /// <see cref="UnitTestExAspireExtensions.WaitForResourceAsync(DistributedApplication, string[], TimeSpan?)"/> extension method instead.</para></remarks>
        public async Task WaitForResourceAsync(string[] resourceNames, TimeSpan? timeout = null)
        {
            if (resourceNames is null) throw new ArgumentNullException(nameof(resourceNames));

            await Task.WhenAll(resourceNames.Distinct().Select(rn => WaitForResourceAsync(rn, timeout))).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        /// <remarks>Combines the elapsed log messages captured (via <see cref="ResourceLogCaptureProvider"/>) across <i>all</i> resources since the last invocation, as background/inter-resource
        /// activity is not necessarily confined to a single resource; each line already carries its own owning resource name as a trailing suffix (see <see cref="ResourceLogCaptureProvider"/>),
        /// so no further attribution is added here.
        /// <para>Also where <see cref="ErrorWhenLogContainsMinimumLevel"/> is set (see <see cref="AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/>), every entry
        /// drained here - across every resource, not just one directly interacted with - is checked and immediately fails the test if it violates it; a resource that never has an
        /// <see cref="Http(string, string?)"/> call made against it (e.g. a background/hosted-service-only resource) is still checked here, as long as a subsequent
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.Checkpoint"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.Delay(TimeSpan?, string?)"/> call drains it - hence a final
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.Checkpoint"/> call is recommended at the end of a test to verify whatever log activity remains un-drained.</para></remarks>
        protected override IEnumerable<string?>? DrainElapsedLogMessages()
        {
            _resourceLogCaptureProvider?.FlushPendingEntries();

            var lines = new List<string?>();
            foreach (var (resourceName, buffer) in _resourceLogBuffers)
            {
                var lastCount = _elapsedLogLineCounts.GetOrAdd(resourceName, 0);
                var newEntries = buffer.Skip(lastCount).ToArray();
                _elapsedLogLineCounts[resourceName] = buffer.Count;

                AssertNoErrorWhenLogContainsViolation(newEntries, resourceName);
                lines.AddRange(newEntries.Select(e => e.Text));
            }

            return lines;
        }

        /// <summary>
        /// Fails the test (via <see cref="TesterBaseCore.Implementor"/>) on the first <paramref name="entries"/> entry at or above <see cref="ErrorWhenLogContainsMinimumLevel"/> that (where
        /// any <see cref="ErrorWhenLogContainsIncludePatterns"/> are configured) matches at least one of them, and does not match any of the wildcard
        /// <see cref="ErrorWhenLogContainsExcludePatterns"/> - a no-op where <see cref="ErrorWhenLogContainsMinimumLevel"/> is <see cref="LogLevel.None"/> (the explicit opt-out), since no
        /// entry can ever meet or exceed it.
        /// </summary>
        private void AssertNoErrorWhenLogContainsViolation(IEnumerable<ResourceLogEntry> entries, string resourceName)
        {
            foreach (var entry in entries)
            {
                if (entry.Level < ErrorWhenLogContainsMinimumLevel)
                    continue;

                if (ErrorWhenLogContainsIncludePatterns.Count > 0 && !ErrorWhenLogContainsIncludePatterns.Any(pattern => IsWildcardMatch(entry.Text, pattern)))
                    continue;

                if (ErrorWhenLogContainsExcludePatterns.Any(pattern => IsWildcardMatch(entry.Text, pattern)))
                    continue;

                Implementor.AssertFail($"Resource '{resourceName}' logged an entry at '{entry.Level}' level, which is at or above the '{ErrorWhenLogContainsMinimumLevel}' level configured via ErrorWhenLogContains: {entry.Text}");
            }
        }

        /// <summary>
        /// Determines whether <paramref name="text"/> contains <paramref name="pattern"/> - a simple <c>*</c> (any run of characters)/<c>?</c> (any single character) wildcard match,
        /// case-insensitive, applied anywhere within <paramref name="text"/> (i.e. a wildcard-aware equivalent of <see cref="string.Contains(string, StringComparison)"/>).
        /// </summary>
        private static bool IsWildcardMatch(string? text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            var regexPattern = string.Join(".*", pattern.Split('*').Select(segment => Regex.Escape(segment).Replace(@"\?", ".")));
            return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        /// <summary>
        /// Enables a test <see cref="System.Net.Http.HttpRequestMessage"/> to be sent to the named resource.
        /// </summary>
        /// <param name="resourceName">The resource name (as configured within the AppHost) to target.</param>
        /// <param name="endpointName">The optional endpoint name. Where not specified, the "https" endpoint is preferred when available, falling back to "http" (see <see cref="DistributedApplicationHostingTestingExtensions.CreateHttpClient(DistributedApplication, string, string?)"/>).</param>
        /// <returns>The <see cref="HttpTester"/>.</returns>
        public HttpTester Http(string resourceName, string? endpointName = null) => new(this, new ResourceHttpClientSource(this, resourceName, endpointName));

        /// <summary>
        /// Enables a test <see cref="System.Net.Http.HttpRequestMessage"/> to be sent to the named resource with an expected response value <see cref="System.Type"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response value <see cref="System.Type"/>.</typeparam>
        /// <param name="resourceName">The resource name (as configured within the AppHost) to target.</param>
        /// <param name="endpointName">The optional endpoint name. Where not specified, the "https" endpoint is preferred when available, falling back to "http" (see <see cref="DistributedApplicationHostingTestingExtensions.CreateHttpClient(DistributedApplication, string, string?)"/>).</param>
        /// <returns>The <see cref="HttpTester{TResponse}"/>.</returns>
        public HttpTester<TResponse> Http<TResponse>(string resourceName, string? endpointName = null) => new(this, new ResourceHttpClientSource(this, resourceName, endpointName));

        /// <summary>
        /// Enables a fluent <see cref="HttpMock.AspireHttpMockClient"/> to stub HTTP responses from a real, out-of-process WireMock.Net server resource, for mocking external/third-party
        /// HTTP dependencies within a Tier 2 test.
        /// </summary>
        /// <param name="resourceName">The WireMock.Net server resource name (as configured within the AppHost) to target.</param>
        /// <param name="endpointName">The optional endpoint name; where not specified, the resource's default endpoint is used.</param>
        /// <returns>The <see cref="HttpMock.AspireHttpMockClient"/>.</returns>
        /// <remarks>Works against <b>any</b> resource type exposing a WireMock.Net admin API endpoint - most commonly a self-hosted, ordinary Aspire project resource (a small,
        /// self-contained sample the consumer copies into their own solution; see UnitTestEx's README "Aspire multi-host testing" section for the ~20-line template) - UnitTestEx's
        /// recommended default, as it needs no Docker/Podman and unlocks <see cref="HttpMock.AspireHttpMockRequest.WithJsonBodyUsingUnitTestExComparer(string, string[])"/> for genuine
        /// JSON comparison-semantics parity with Tier 1. It equally works against the official <c>WireMock.Net.Aspire</c> package's container resource (<c>builder.AddWireMock(name)</c>),
        /// for teams already standardized on that package - though its <c>JsonMatcher</c>/<c>JsonPartialMatcher</c> only offer WireMock.Net's own JSON comparison semantics (see
        /// <see cref="HttpMock.AspireHttpMockRequest.WithJsonBody(string, string[])"/> remarks).
        /// <para>The admin API <see cref="WireMock.Client.IWireMockAdminApi"/> client is built directly from a resolved <see cref="HttpClient"/> (the same resolution path
        /// <see cref="Http(string, string?)"/> uses), rather than via the official package's own resource-type-specific helper - which keeps this resource-type-agnostic.</para>
        /// <para>This tester's current <see cref="TesterBaseCore.JsonComparerOptions"/> (see <see cref="AspireTesterBase{TAppHost, TSelf}.UseJsonComparerOptions"/>) is captured live at the point
        /// <see cref="HttpMock.AspireHttpMockRequest.WithJsonBodyUsingUnitTestExComparer(string, string[])"/> is subsequently called - not at this method's call time - so a prior
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.UseJsonComparerOptions"/> call is always honoured.</para></remarks>
        public HttpMock.AspireHttpMockClient HttpMock(string resourceName, string? endpointName = null)
        {
            if (resourceName is null) throw new ArgumentNullException(nameof(resourceName));

            return new HttpMock.AspireHttpMockClient(async () =>
            {
                var app = await GetDistributedApplicationAsync().ConfigureAwait(false);
                var httpClient = app.CreateHttpClient(resourceName, endpointName);
                return RestEase.RestClient.For<WireMock.Client.IWireMockAdminApi>(httpClient);
            }, () => JsonComparerOptions);
        }

        /// <inheritdoc/>
        /// <remarks>The <paramref name="name"/> is required as, unlike a single-host Tier 1 tester, there is no single default resource to fall back to; prefer <see cref="Http(string, string?)"/>/
        /// <see cref="Http{TResponse}(string, string?)"/> which bind the resource name for you.</remarks>
        HttpClient IHttpClientSource.CreateHttpClient(string? name) =>
            GetDistributedApplicationAsync().GetAwaiter().GetResult().CreateHttpClient(name ?? throw new ArgumentNullException(nameof(name)));

        /// <summary>
        /// An <see cref="ILoggerProvider"/> that captures each resource's own forwarded logging (see <c>EnableResourceLogging</c>) - logged by the AppHost under a
        /// "{ApplicationName}.Resources.{resourceName}" category - into an in-memory, per-resource buffer, reformatted to match the standard UnitTestEx <see cref="Logging.LoggerBase"/>
        /// output style (see <see cref="ResourceLogger.FlushPending"/>).
        /// </summary>
        /// <remarks>Under <see cref="DistributedApplicationTestingBuilder"/>, <see cref="ResourceLoggerService.GetAllAsync(string)"/>/<see cref="ResourceLoggerService.WatchAsync(string)"/>
        /// are not populated (a known limitation of the testing host), so this is the only reliable way to observe a resource's own logging from a test.</remarks>
        private sealed class ResourceLogCaptureProvider(string resourceCategoryPrefix, ConcurrentDictionary<string, ConcurrentQueue<ResourceLogEntry>> buffers) : ILoggerProvider
        {
            private readonly ConcurrentBag<ResourceLogger> _loggers = [];

            public ILogger CreateLogger(string categoryName)
            {
                if (!categoryName.StartsWith(resourceCategoryPrefix, StringComparison.Ordinal))
                    return NullLogger.Instance;

                var resourceName = categoryName[resourceCategoryPrefix.Length..];
                var logger = new ResourceLogger(resourceName, buffers.GetOrAdd(resourceName, _ => new ConcurrentQueue<ResourceLogEntry>()));
                _loggers.Add(logger);
                return logger;
            }

            /// <summary>
            /// Flushes any pending (not-yet-terminated by a subsequent header line) entry for every resource.
            /// </summary>
            /// <remarks>An entry only completes once <i>another</i> line (typically the next header) arrives to terminate it, so the most recently logged entry for a resource would otherwise remain
            /// invisible until something else happens to log afterwards; callers that need to observe "everything captured so far" (e.g. before reading a resource's log buffer) must force this.</remarks>
            public void FlushPendingEntries()
            {
                foreach (var logger in _loggers)
                {
                    logger.FlushPending();
                }
            }

            /// <summary>
            /// Flushes any pending entry for every resource, so a final log line is not lost when the underlying <see cref="DistributedApplication"/> is disposed mid-entry.
            /// </summary>
            public void Dispose() => FlushPendingEntries();

            /// <summary>
            /// Parses and reformats each resource's forwarded, already console-rendered text into the standard UnitTestEx <see cref="Logging.LoggerBase"/> output style, i.e.
            /// "<c>{timestamp} {level}: {message} [{category} ({resourceName})]</c>".
            /// </summary>
            /// <remarks>The AppHost forwards each resource's own console output one <i>physical line</i> at a time (e.g. the "<c>{level}: {category}[{eventId}]</c>" header line, then one or more
            /// indented message lines, as separate <see cref="Log{TState}"/> calls), each prefixed with a "<c>{lineNumber}: {timestamp}Z </c>" marker that Aspire itself adds; this reassembles those
            /// physical lines back into a single logical entry using the same style as an in-process (Tier 1) tester, re-using Aspire's own embedded timestamp (converted to local time) rather than
            /// substituting the capture time, so timestamps reflect when the resource itself actually logged the entry. The owning resource name is appended, in parentheses, inside the same
            /// trailing "<c>[...]</c>" bracket as the category - rather than as a separate leading prefix - so the timestamp/level/message stay column-aligned whether read via a single-resource
            /// (<see cref="Http(string, string?)"/>) or multi-resource (<see cref="AspireTesterBase{TAppHost, TSelf}.Delay(TimeSpan?, string?)"/>) report.
            /// <para>A line that does not match the expected "<c>{lineNumber}: {timestamp}Z </c>" prefix (e.g. a resource that does not use the standard console logger format) is passed through
            /// unmodified, ANSI colour codes aside, so nothing is silently dropped.</para></remarks>
            private sealed class ResourceLogger(string resourceName, ConcurrentQueue<ResourceLogEntry> buffer) : ILogger
            {
                private static readonly Regex _ansiPattern = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);
                private static readonly Regex _linePrefixPattern = new(@"^\d+:\s+(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)Z\s?(?<rest>.*)$", RegexOptions.Compiled);
                private static readonly Regex _headerPattern = new(@"^(?<level>trce|dbug|info|warn|fail|crit): (?<category>.+)\[(?<eventId>-?\d+)\]$", RegexOptions.Compiled);

#if NET9_0_OR_GREATER
                private readonly Lock _lock = new();
#else
                private readonly object _lock = new();
#endif
                private string? _pendingTimestamp;
                private string? _pendingLevelText;
                private LogLevel _pendingLevel;
                private string? _pendingCategory;
                private readonly List<string> _pendingMessageLines = [];

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    var line = _ansiPattern.Replace(formatter(state, exception), string.Empty);
                    var prefixMatch = _linePrefixPattern.Match(line);

                    lock (_lock)
                    {
                        if (!prefixMatch.Success)
                        {
                            // Not a recognized Aspire-forwarded console line; flush anything pending and pass this through as-is rather than silently dropping it. Its own severity cannot be
                            // determined (there is no "{level}: " header to parse it from), so it is treated as Information - safely below any sensible ErrorWhenLogContains threshold.
                            FlushPendingNoLock();
                            buffer.Enqueue(new ResourceLogEntry(LogLevel.Information, line));
                            return;
                        }

                        var rest = prefixMatch.Groups["rest"].Value;
                        var headerMatch = _headerPattern.Match(rest);
                        if (headerMatch.Success)
                        {
                            // A new header line always terminates any previously pending entry.
                            FlushPendingNoLock();
                            _pendingTimestamp = prefixMatch.Groups["ts"].Value;
                            _pendingLevelText = headerMatch.Groups["level"].Value;
                            _pendingLevel = ParseLevel(_pendingLevelText);
                            _pendingCategory = headerMatch.Groups["category"].Value;
                        }
                        else if (_pendingTimestamp is not null)
                            _pendingMessageLines.Add(rest.TrimStart()); // A message/continuation line for the currently pending header.
                        else
                            buffer.Enqueue(new ResourceLogEntry(LogLevel.Information, rest)); // A line with no preceding header (unexpected); pass through as-is.
                    }
                }

                /// <summary>
                /// Maps Aspire's forwarded console-formatter level abbreviation (see <see cref="_headerPattern"/>) back to its corresponding <see cref="LogLevel"/> - the exact reverse of the
                /// standard ASP.NET Core console formatter's own abbreviation convention.
                /// </summary>
                private static LogLevel ParseLevel(string levelText) => levelText switch
                {
                    "trce" => LogLevel.Trace,
                    "dbug" => LogLevel.Debug,
                    "info" => LogLevel.Information,
                    "warn" => LogLevel.Warning,
                    "fail" => LogLevel.Error,
                    "crit" => LogLevel.Critical,
                    _ => LogLevel.Information
                };

                /// <summary>
                /// Flushes any pending entry to the buffer, reformatted to match <see cref="Logging.LoggerBase"/>'s output style.
                /// </summary>
                public void FlushPending()
                {
                    lock (_lock)
                    {
                        FlushPendingNoLock();
                    }
                }

                private void FlushPendingNoLock()
                {
                    if (_pendingTimestamp is null)
                        return;

                    var ts = DateTime.Parse(_pendingTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToLocalTime();
                    var sb = new StringBuilder();
                    sb.Append($"{ts.ToString("yyyy-MM-ddTHH:mm:ss.fffff", DateTimeFormatInfo.InvariantInfo)} {_pendingLevelText}: {(_pendingMessageLines.Count > 0 ? _pendingMessageLines[0] : string.Empty)} [{_pendingCategory} ({resourceName})]");
                    for (var i = 1; i < _pendingMessageLines.Count; i++)
                    {
                        sb.AppendLine();
                        sb.Append($"{new string(' ', 32)}{_pendingMessageLines[i]}");
                    }

                    buffer.Enqueue(new ResourceLogEntry(_pendingLevel, sb.ToString()));

                    _pendingTimestamp = null;
                    _pendingLevelText = null;
                    _pendingCategory = null;
                    _pendingMessageLines.Clear();
                }
            }
        }

        /// <summary>
        /// A single captured resource log entry - the parsed <see cref="Microsoft.Extensions.Logging.LogLevel"/> (see <see cref="ErrorWhenLogContainsMinimumLevel"/>) alongside the fully
        /// formatted text (matching <see cref="Logging.LoggerBase"/>'s output style) that every existing consumer (<see cref="DrainElapsedLogMessages"/>, <see cref="ResourceHttpClientSource"/>)
        /// continues to see.
        /// </summary>
        private readonly record struct ResourceLogEntry(LogLevel Level, string? Text);

        /// <summary>
        /// An <see cref="IHttpClientSource"/> bound to a specific named resource (and optional endpoint).
        /// </summary>
        /// <remarks>Piggy-backs the resource's own forwarded logging (captured via <see cref="ResourceLogCaptureProvider"/>) into the same "LOGGING &gt;" section of the tester output that a
        /// Tier 1, in-process host correlates via <see cref="TestSharedState.GetLoggerMessages(string?)"/>; as there is no shared, in-process container to correlate by request identifier, the
        /// window of lines captured between <see cref="CreateHttpClient(string?)"/> (invoked immediately before the request is sent) and <see cref="GetRequestLogMessages(string)"/> (invoked
        /// immediately after the response is received) is used as a pragmatic proxy - this naturally excludes the resource's own start-up banner noise (already buffered by the time the first
        /// request is sent) and correlates correctly for the typical, sequential one-request-at-a-time usage pattern. The window's baseline/end-point is tracked via the <i>same</i> shared,
        /// per-resource watermark (<c>_elapsedLogLineCounts</c>) that <see cref="DrainElapsedLogMessages"/> (used by <see cref="AspireTesterBase{TAppHost, TSelf}.Delay(TimeSpan?, string?)"/>) advances, so a resource
        /// log line reported here is claimed and will not also be re-reported by a subsequent <see cref="AspireTesterBase{TAppHost, TSelf}.Delay(TimeSpan?, string?)"/> call (or vice versa).</remarks>
        private sealed class ResourceHttpClientSource(AspireTesterBase owner, string resourceName, string? endpointName) : IHttpClientSource
        {
            public HttpClient CreateHttpClient(string? name = null)
            {
                var app = owner.GetDistributedApplicationAsync().GetAwaiter().GetResult();

                // Flush any pending (not-yet-terminated) entry first so it is counted in the baseline rather than leaking into this request's own captured window, then establish the shared
                // watermark at the current buffer length (i.e. claim everything already buffered as "seen", not part of this request's own window).
                owner._resourceLogCaptureProvider?.FlushPendingEntries();
                owner._elapsedLogLineCounts[resourceName] = owner._resourceLogBuffers.TryGetValue(resourceName, out var buffer) ? buffer.Count : 0;
                return app.CreateHttpClient(resourceName, endpointName);
            }

            public IEnumerable<string?>? GetRequestLogMessages(string requestId) => GetNewResourceLogMessagesAsync().GetAwaiter().GetResult();

            /// <summary>
            /// Gets the resource's own log lines captured since <see cref="CreateHttpClient(string?)"/> was last invoked, advancing the shared watermark past them so they are claimed.
            /// </summary>
            /// <remarks>The resource's logging is forwarded asynchronously; a single short grace period is allowed for it to catch up before giving up, rather than an open-ended poll that would
            /// otherwise tax every request - including the (typical) majority that log nothing at all.</remarks>
            private async Task<IReadOnlyList<string?>> GetNewResourceLogMessagesAsync()
            {
                if (!owner._resourceLogBuffers.TryGetValue(resourceName, out var buffer))
                    return [];

                var baseline = owner._elapsedLogLineCounts.GetOrAdd(resourceName, buffer.Count);

                if (buffer.Count <= baseline)
                    await Task.Delay(300).ConfigureAwait(false);

                // An entry only becomes visible in the buffer once terminated by a subsequent header line; force-flush whatever is currently pending so the most recently logged entry
                // (often the interesting one, e.g. the endpoint's own logging for this very request) is not lost while waiting for a line that may never come.
                owner._resourceLogCaptureProvider?.FlushPendingEntries();

                var newEntries = buffer.Skip(baseline).ToArray();
                owner._elapsedLogLineCounts[resourceName] = buffer.Count;

                owner.AssertNoErrorWhenLogContainsViolation(newEntries, resourceName);
                return newEntries.Select(e => e.Text).ToArray();
            }
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        /// <remarks>Sealed by design - a derived tester that needs to release its own resources should override <see cref="DisposeAsyncCore"/> instead (matching the standard
        /// .NET async-dispose pattern), not this method.</remarks>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            await DisposeAsyncCore().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the underlying <see cref="DistributedApplication"/> and any other managed resources.
        /// </summary>
        /// <remarks>Override this - not <see cref="DisposeAsync"/> - to release additional resources in a derived tester; always call <c>await base.DisposeAsyncCore()</c> to
        /// ensure the underlying <see cref="DistributedApplication"/> is still released.</remarks>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            Task<DistributedApplication>? appTask;
            lock (SyncRoot)
            {
                appTask = _appTask;
                _appTask = null;
            }

            if (appTask is null)
                return;

            try
            {
                var app = await appTask.ConfigureAwait(false);
                await app.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Swallow; there is nothing meaningful to dispose where the underlying host failed to build/start.
            }
        }
    }
}
