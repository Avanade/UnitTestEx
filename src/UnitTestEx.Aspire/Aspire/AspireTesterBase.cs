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
    /// Provides the base .NET Aspire multi-host (distributed application) unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
    /// <typeparam name="TSelf">The <see cref="AspireTesterBase{TAppHost, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    /// <remarks>This extends the host-agnostic <see cref="TesterBaseCore"/> directly (<i>not</i> <see cref="TesterBase"/>) as the underlying <see cref="DistributedApplication"/> is a real,
    /// multi-process distributed application with no single, in-process dependency injection (DI) container to reach into. Where a Tier 1 (<see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>) capability is genuinely shared in concept, it is exposed here via the
    /// <see cref="IHttpClientSource"/> seam rather than by inheriting <see cref="TesterBase"/>'s dependency injection (DI) specific members.</remarks>
    public abstract class AspireTesterBase<TAppHost, TSelf> : TesterBaseCore, IHttpClientSource, IAsyncDisposable
        where TAppHost : class
        where TSelf : AspireTesterBase<TAppHost, TSelf>
    {
        private readonly List<Action<IDistributedApplicationTestingBuilder>> _configureBuilder = [];
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string?>> _resourceLogBuffers = new();
        private readonly ConcurrentDictionary<string, int> _elapsedLogLineCounts = new();
        private ResourceLogCaptureProvider? _resourceLogCaptureProvider;
        private Task<DistributedApplication>? _appTask;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireTesterBase{TAppHost, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected AspireTesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <summary>
        /// Gets the <see cref="DistributedApplication"/>; builds and starts on first access.
        /// </summary>
        /// <returns>The started <see cref="DistributedApplication"/>.</returns>
        protected Task<DistributedApplication> GetDistributedApplicationAsync()
        {
            lock (SyncRoot)
            {
                return _appTask ??= CreateDistributedApplicationAsync();
            }
        }

        /// <summary>
        /// Creates, builds and starts the underlying <see cref="DistributedApplication"/>.
        /// </summary>
        private async Task<DistributedApplication> CreateDistributedApplicationAsync()
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<TAppHost>().ConfigureAwait(false);

            if (!HostDiagnosticsEnabled)
                // By default the AppHost's own logging (its start-up banner, DCP process management, and - critically - each resource's own console output mirrored under a
                // "{ApplicationName}.Resources.{resourceName}" category; see 'EnableResourceLogging') writes straight to the test's Standard Output via its default provider(s) (e.g. console),
                // duplicating what UnitTestEx already captures and reports cleanly (and correlated) via Reason()/Wait()/the Http() request-scoped "LOGGING >" section. Remove the AppHost's
                // own provider(s) so only what UnitTestEx explicitly writes reaches the test output; call EnableHostDiagnostics() before this point to opt back into the raw firehose (e.g. when
                // troubleshooting an AppHost/resource start-up failure).
                builder.Services.RemoveAll<ILoggerProvider>();

            builder.Services.Configure<LoggerFilterOptions>(options =>
                // Exempt our own capture provider from any ambient filtering (by provider, not category) so it always sees everything, regardless of the AppHost's own console noise level
                // (which the AppHost's own default logging configuration governs independently and is not something UnitTestEx overrides).
                options.Rules.Add(new LoggerFilterRule(typeof(ResourceLogCaptureProvider).FullName, null, LogLevel.Trace, null)));

            builder.Services.AddSingleton<ILoggerProvider>(_resourceLogCaptureProvider = new ResourceLogCaptureProvider($"{builder.Environment.ApplicationName}.Resources.", _resourceLogBuffers));

            foreach (var configure in _configureBuilder)
            {
                configure(builder);
            }

            OnHostStartUp();

            var app = await builder.BuildAsync().ConfigureAwait(false);

            try
            {
                await app.StartAsync().ConfigureAwait(false);
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return app;
        }

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
        /// Overrides an environment variable for the named resource before the underlying <see cref="DistributedApplication"/> is built.
        /// </summary>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="key">The environment variable name.</param>
        /// <param name="value">The environment variable value.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This is the only override surface available for a Tier 2 tester as each resource runs in its own, separate process with no shared, in-process dependency injection
        /// (DI) container to reach into; must be called before the underlying <see cref="DistributedApplication"/> has been built (see <see cref="GetDistributedApplicationAsync"/>).</remarks>
        public TSelf WithResourceEnvironment(string resourceName, string key, string value)
        {
            if (resourceName is null) throw new ArgumentNullException(nameof(resourceName));
            if (key is null) throw new ArgumentNullException(nameof(key));

            lock (SyncRoot)
            {
                if (_appTask is not null)
                    throw new InvalidOperationException($"{nameof(WithResourceEnvironment)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                _configureBuilder.Add(builder => builder.CreateResourceBuilder<IResourceWithEnvironment>(resourceName).WithEnvironment(key, value));
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Gets a value indicating whether the AppHost's own raw logging (its start-up banner, DCP process management, and each resource's own console output mirrored via
        /// 'EnableResourceLogging') is written to the test output, in addition to what UnitTestEx itself reports (see <see cref="EnableHostDiagnostics"/>).
        /// </summary>
        public bool HostDiagnosticsEnabled { get; private set; }

        /// <summary>
        /// Opts back into the AppHost's own raw logging (its start-up banner, DCP process management, and each resource's own console output) being written to the test output, in addition
        /// to what UnitTestEx itself reports via <see cref="Reason"/>/<see cref="Wait"/>/the <see cref="Http(string, string?)"/> request-scoped "LOGGING &gt;" section.
        /// </summary>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>By default this raw firehose is suppressed as it otherwise duplicates what UnitTestEx already surfaces cleanly (and correlated); enable it when troubleshooting an
        /// AppHost/resource start-up failure that needs the full, unfiltered diagnostic output. Must be called before the underlying <see cref="DistributedApplication"/> has been built
        /// (see <see cref="GetDistributedApplicationAsync"/>).</remarks>
        public TSelf EnableHostDiagnostics()
        {
            lock (SyncRoot)
            {
                if (_appTask is not null)
                    throw new InvalidOperationException($"{nameof(EnableHostDiagnostics)} must be invoked before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                HostDiagnosticsEnabled = true;
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Gets the default timeout used by <see cref="WaitForResourceAsync(string, TimeSpan?)"/> when none is specified.
        /// </summary>
        public static TimeSpan DefaultWaitForResourceTimeout { get; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Waits for the named resource to report a healthy status.
        /// </summary>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="timeout">The timeout (defaults to <see cref="DefaultWaitForResourceTimeout"/>); pass <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <remarks>A resource that never becomes healthy (e.g. a misconfigured health check) should fail the test fast rather than hang it indefinitely, hence the default timeout.</remarks>
        public async Task WaitForResourceAsync(string resourceName, TimeSpan? timeout = null)
        {
            if (resourceName is null) throw new ArgumentNullException(nameof(resourceName));

            var effectiveTimeout = timeout ?? DefaultWaitForResourceTimeout;
            var app = await GetDistributedApplicationAsync().ConfigureAwait(false);
            var wait = app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName);
            await (effectiveTimeout == System.Threading.Timeout.InfiniteTimeSpan ? wait : wait.WaitAsync(effectiveTimeout)).ConfigureAwait(false);
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
        /// Waits for the specified <paramref name="duration"/>, then writes any resource log messages captured (across <i>all</i> resources) during that time to the test output.
        /// </summary>
        /// <param name="reason">The reason for waiting (written to the test output for context); defaults to "Not specified." where not specified.</param>
        /// <param name="duration">The duration to wait; defaults to <see cref="TesterBaseCore.DefaultWaitDuration"/> where not specified.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Useful when waiting on background/inter-resource activity (e.g. a message being processed by a downstream resource) that is not tied to a specific HTTP request/response (see
        /// <see cref="Http(string, string?)"/> for the latter, request-scoped, correlation).</remarks>
        public TSelf Wait(string? reason = null, TimeSpan? duration = null) => WriteWait(reason, duration).ContinueWith(_ => (TSelf)this).Result;

        /// <inheritdoc/>
        /// <remarks>Combines the elapsed log messages captured (via <see cref="ResourceLogCaptureProvider"/>) across <i>all</i> resources since the last invocation, as background/inter-resource
        /// activity is not necessarily confined to a single resource; each line already carries its own owning resource name as a trailing suffix (see <see cref="ResourceLogCaptureProvider"/>),
        /// so no further attribution is added here.</remarks>
        protected override IEnumerable<string?>? DrainElapsedLogMessages()
        {
            _resourceLogCaptureProvider?.FlushPendingEntries();

            var lines = new List<string?>();
            foreach (var (resourceName, buffer) in _resourceLogBuffers)
            {
                var lastCount = _elapsedLogLineCounts.GetOrAdd(resourceName, 0);
                var newLines = buffer.Skip(lastCount).ToArray();
                _elapsedLogLineCounts[resourceName] = buffer.Count;

                lines.AddRange(newLines);
            }

            return lines;
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
        /// Enables a fluent <see cref="HttpMock.AspireHttpMockClient"/> to stub HTTP responses from a WireMock.Net server resource (added via the official <c>WireMock.Net.Aspire</c> package's
        /// <c>builder.AddWireMock(name)</c> in the AppHost), for mocking external/third-party HTTP dependencies within a Tier 2 test.
        /// </summary>
        /// <param name="resourceName">The WireMock.Net server resource name (as configured within the AppHost) to target.</param>
        /// <param name="endpointName">The optional endpoint name; where not specified, the resource's single default endpoint is used (see <c>DistributedApplicationExtensions.CreateWireMockAdminClient</c>).</param>
        /// <returns>The <see cref="HttpMock.AspireHttpMockClient"/>.</returns>
        public HttpMock.AspireHttpMockClient HttpMock(string resourceName, string? endpointName = null)
        {
            if (resourceName is null) throw new ArgumentNullException(nameof(resourceName));

            return new HttpMock.AspireHttpMockClient(async () =>
            {
                var app = await GetDistributedApplicationAsync().ConfigureAwait(false);
                return app.CreateWireMockAdminClient(resourceName, endpointName);
            });
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
        private sealed class ResourceLogCaptureProvider(string resourceCategoryPrefix, ConcurrentDictionary<string, ConcurrentQueue<string?>> buffers) : ILoggerProvider
        {
            private readonly ConcurrentBag<ResourceLogger> _loggers = [];

            public ILogger CreateLogger(string categoryName)
            {
                if (!categoryName.StartsWith(resourceCategoryPrefix, StringComparison.Ordinal))
                    return NullLogger.Instance;

                var resourceName = categoryName[resourceCategoryPrefix.Length..];
                var logger = new ResourceLogger(resourceName, buffers.GetOrAdd(resourceName, _ => new ConcurrentQueue<string?>()));
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
            /// (<see cref="Http(string, string?)"/>) or multi-resource (<see cref="Wait"/>) report.
            /// <para>A line that does not match the expected "<c>{lineNumber}: {timestamp}Z </c>" prefix (e.g. a resource that does not use the standard console logger format) is passed through
            /// unmodified, ANSI colour codes aside, so nothing is silently dropped.</para></remarks>
            private sealed class ResourceLogger(string resourceName, ConcurrentQueue<string?> buffer) : ILogger
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
                private string? _pendingLevel;
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
                            // Not a recognized Aspire-forwarded console line; flush anything pending and pass this through as-is rather than silently dropping it.
                            FlushPendingNoLock();
                            buffer.Enqueue(line);
                            return;
                        }

                        var rest = prefixMatch.Groups["rest"].Value;
                        var headerMatch = _headerPattern.Match(rest);
                        if (headerMatch.Success)
                        {
                            // A new header line always terminates any previously pending entry.
                            FlushPendingNoLock();
                            _pendingTimestamp = prefixMatch.Groups["ts"].Value;
                            _pendingLevel = headerMatch.Groups["level"].Value;
                            _pendingCategory = headerMatch.Groups["category"].Value;
                        }
                        else if (_pendingTimestamp is not null)
                            _pendingMessageLines.Add(rest.TrimStart()); // A message/continuation line for the currently pending header.
                        else
                            buffer.Enqueue(rest); // A line with no preceding header (unexpected); pass through as-is.
                    }
                }

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
                    sb.Append($"{ts.ToString("yyyy-MM-ddTHH:mm:ss.fffff", DateTimeFormatInfo.InvariantInfo)} {_pendingLevel}: {(_pendingMessageLines.Count > 0 ? _pendingMessageLines[0] : string.Empty)} [{_pendingCategory} ({resourceName})]");
                    for (var i = 1; i < _pendingMessageLines.Count; i++)
                    {
                        sb.AppendLine();
                        sb.Append($"{new string(' ', 32)}{_pendingMessageLines[i]}");
                    }

                    buffer.Enqueue(sb.ToString());

                    _pendingTimestamp = null;
                    _pendingLevel = null;
                    _pendingCategory = null;
                    _pendingMessageLines.Clear();
                }
            }
        }

        /// <summary>
        /// An <see cref="IHttpClientSource"/> bound to a specific named resource (and optional endpoint).
        /// </summary>
        /// <remarks>Piggy-backs the resource's own forwarded logging (captured via <see cref="ResourceLogCaptureProvider"/>) into the same "LOGGING &gt;" section of the tester output that a
        /// Tier 1, in-process host correlates via <see cref="TestSharedState.GetLoggerMessages(string?)"/>; as there is no shared, in-process container to correlate by request identifier, the
        /// window of lines captured between <see cref="CreateHttpClient(string?)"/> (invoked immediately before the request is sent) and <see cref="GetRequestLogMessages(string)"/> (invoked
        /// immediately after the response is received) is used as a pragmatic proxy - this naturally excludes the resource's own start-up banner noise (already buffered by the time the first
        /// request is sent) and correlates correctly for the typical, sequential one-request-at-a-time usage pattern. The window's baseline/end-point is tracked via the <i>same</i> shared,
        /// per-resource watermark (<see cref="_elapsedLogLineCounts"/>) that <see cref="DrainElapsedLogMessages"/> (used by <see cref="Wait"/>) advances, so a resource log line reported
        /// here is claimed and will not also be re-reported by a subsequent <see cref="Wait"/> call (or vice versa).</remarks>
        private sealed class ResourceHttpClientSource(AspireTesterBase<TAppHost, TSelf> owner, string resourceName, string? endpointName) : IHttpClientSource
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

                var newLines = buffer.Skip(baseline).ToArray();
                owner._elapsedLogLineCounts[resourceName] = buffer.Count;

                return newLines;
            }
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

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
