// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
        /// An <see cref="IHttpClientSource"/> bound to a specific named resource (and optional endpoint).
        /// </summary>
        private sealed class ResourceHttpClientSource(AspireTesterBase<TAppHost, TSelf> owner, string resourceName, string? endpointName) : IHttpClientSource
        {
            public HttpClient CreateHttpClient(string? name = null) => owner.GetDistributedApplicationAsync().GetAwaiter().GetResult().CreateHttpClient(resourceName, endpointName);
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
