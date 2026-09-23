// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
    /// multi-process distributed application with no single, in-process dependency injection (DI) container to reach into; see the <c>docs/design/aspire-multi-host-testing.md</c> design
    /// note for the rationale. Where a Tier 1 (<see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>) capability is genuinely shared in concept, it is exposed here via the
    /// <see cref="IHttpClientSource"/> seam rather than by inheriting <see cref="TesterBase"/>'s dependency injection (DI) specific members.</remarks>
    public abstract class AspireTesterBase<TAppHost, TSelf> : TesterBaseCore, IHttpClientSource, IAsyncDisposable
        where TAppHost : class
        where TSelf : AspireTesterBase<TAppHost, TSelf>
    {
        private readonly List<Action<IDistributedApplicationTestingBuilder>> _configureBuilder = [];
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string?>> _resourceLogBuffers = new();
        private Task<DistributedApplication>? _appTask;
        private LogLevel _minimumLogLevel = LogLevel.Warning;
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
        /// Gets or sets the minimum <see cref="LogLevel"/> for the underlying AppHost's own logging - i.e. Aspire's hosting/DCP bootstrap messages and the console-log relay
        /// it forwards for each resource (not the resources' own internal logging, which is governed by their own configuration); defaults to <see cref="LogLevel.Warning"/> so
        /// that this Aspire-internal chatter does not swamp the UnitTestEx request/response tester output. Must be set before the underlying <see cref="DistributedApplication"/>
        /// has been built (see <see cref="GetDistributedApplicationAsync"/>).
        /// </summary>
        public LogLevel MinimumLogLevel
        {
            get => _minimumLogLevel;
            set
            {
                lock (SyncRoot)
                {
                    if (_appTask is not null)
                        throw new InvalidOperationException($"{nameof(MinimumLogLevel)} must be set before the underlying {nameof(DistributedApplication)} has been built (i.e. before any {nameof(Http)}/{nameof(WaitForResourceAsync)} call).");

                    _minimumLogLevel = value;
                }
            }
        }

        /// <summary>
        /// Creates, builds and starts the underlying <see cref="DistributedApplication"/>.
        /// </summary>
        private async Task<DistributedApplication> CreateDistributedApplicationAsync()
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<TAppHost>().ConfigureAwait(false);

            builder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.MinLevel = _minimumLogLevel;

                // The AppHost forwards each resource's own logging (e.g. its own ILogger writes) under a "{ApplicationName}.Resources.{resourceName}" category (see 'EnableResourceLogging');
                // exempt our own capture provider from the MinimumLogLevel filter above (by provider, not category) so it always sees everything, regardless of the visible console noise level.
                options.Rules.Add(new LoggerFilterRule(typeof(ResourceLogCaptureProvider).FullName, null, LogLevel.Trace, null));
            });

            builder.Services.AddSingleton<ILoggerProvider>(new ResourceLogCaptureProvider($"{builder.Environment.ApplicationName}.Resources.", _resourceLogBuffers));

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

        /// <inheritdoc/>
        /// <remarks>The <paramref name="name"/> is required as, unlike a single-host Tier 1 tester, there is no single default resource to fall back to; prefer <see cref="Http(string, string?)"/>/
        /// <see cref="Http{TResponse}(string, string?)"/> which bind the resource name for you.</remarks>
        HttpClient IHttpClientSource.CreateHttpClient(string? name) =>
            GetDistributedApplicationAsync().GetAwaiter().GetResult().CreateHttpClient(name ?? throw new ArgumentNullException(nameof(name)));

        /// <summary>
        /// An <see cref="ILoggerProvider"/> that captures each resource's own forwarded logging (see <c>EnableResourceLogging</c>) - logged by the AppHost under a
        /// "{ApplicationName}.Resources.{resourceName}" category - into an in-memory, per-resource buffer.
        /// </summary>
        /// <remarks>Under <see cref="DistributedApplicationTestingBuilder"/>, <see cref="ResourceLoggerService.GetAllAsync(string)"/>/<see cref="ResourceLoggerService.WatchAsync(string)"/>
        /// are not populated (a known limitation of the testing host), so this is the only reliable way to observe a resource's own logging from a test.</remarks>
        private sealed class ResourceLogCaptureProvider(string resourceCategoryPrefix, ConcurrentDictionary<string, ConcurrentQueue<string?>> buffers) : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName)
            {
                if (!categoryName.StartsWith(resourceCategoryPrefix, StringComparison.Ordinal))
                    return NullLogger.Instance;

                var resourceName = categoryName[resourceCategoryPrefix.Length..];
                return new ResourceLogger(buffers.GetOrAdd(resourceName, _ => new ConcurrentQueue<string?>()));
            }

            public void Dispose() { }

            private sealed class ResourceLogger(ConcurrentQueue<string?> buffer) : ILogger
            {
                // The AppHost forwards each resource's already console-rendered text (ANSI colour codes and a "{lineNumber}: {timestamp} " prefix per physical line); strip both for a clean,
                // readable "LOGGING >" section.
                private static readonly Regex _ansiPattern = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);
                private static readonly Regex _linePrefixPattern = new(@"^\d+:\s+\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z\s?", RegexOptions.Compiled);

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    var message = _ansiPattern.Replace(formatter(state, exception), string.Empty);
                    buffer.Enqueue(_linePrefixPattern.Replace(message, string.Empty));
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
        /// request is sent) and correlates correctly for the typical, sequential one-request-at-a-time usage pattern.</remarks>
        private sealed class ResourceHttpClientSource(AspireTesterBase<TAppHost, TSelf> owner, string resourceName, string? endpointName) : IHttpClientSource
        {
            private int _lastLineCount;

            public HttpClient CreateHttpClient(string? name = null)
            {
                var app = owner.GetDistributedApplicationAsync().GetAwaiter().GetResult();
                _lastLineCount = owner._resourceLogBuffers.TryGetValue(resourceName, out var buffer) ? buffer.Count : 0;
                return app.CreateHttpClient(resourceName, endpointName);
            }

            public IEnumerable<string?>? GetRequestLogMessages(string requestId) => GetNewResourceLogMessagesAsync().GetAwaiter().GetResult();

            /// <summary>
            /// Gets the resource's own log lines captured since <see cref="CreateHttpClient(string?)"/> was last invoked.
            /// </summary>
            /// <remarks>The resource's logging is forwarded asynchronously; a single short grace period is allowed for it to catch up before giving up, rather than an open-ended poll that would
            /// otherwise tax every request - including the (typical) majority that log nothing at all.</remarks>
            private async Task<IReadOnlyList<string?>> GetNewResourceLogMessagesAsync()
            {
                if (!owner._resourceLogBuffers.TryGetValue(resourceName, out var buffer))
                    return [];

                if (buffer.Count <= _lastLineCount)
                    await Task.Delay(300).ConfigureAwait(false);

                return [.. buffer.Skip(_lastLineCount)];
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
