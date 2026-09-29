// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Aspire;
using UnitTestEx.Aspire.HttpMock;
using UnitTestEx.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure; by design.
namespace Aspire.Hosting
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Provides <see cref="IDistributedApplicationBuilder"/>/<see cref="IResourceBuilder{T}"/> extension methods for AppHost projects that use UnitTestEx's recommended
    /// self-hosted WireMock.Net "mockhost" pattern (see the README's "Aspire multi-host testing" section and <c>UnitTestEx.Aspire.MockHost</c>), as well as
    /// <see cref="DistributedApplication"/> extension methods useful from test code (e.g. within a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/
    /// <see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/> callback). Deliberately placed in the <see cref="Aspire.Hosting"/> namespace (matching Aspire's own extension method
    /// convention) so these methods are discoverable alongside <c>AddProject</c>/<c>WithEnvironment</c> without an extra <c>using</c>.
    /// </summary>
    public static class UnitTestExAspireExtensions
    {
        /// <summary>
        /// Adds a self-hosted WireMock.Net project resource (e.g. a copy of <c>UnitTestEx.Aspire.MockHost</c>) - but only when <see cref="IDistributedApplicationBuilder.ExecutionContext"/>
        /// is in run mode (<see cref="DistributedApplicationExecutionContext.IsRunMode"/>). The resource is never added in publish mode, since it is a test-only stand-in for an
        /// external dependency with no real production equivalent and must never appear in a published manifest (see <see cref="WithMockHostEnvironment{TDestination, TSource}"/>
        /// for wiring the resulting endpoint into another resource's environment without a null check at every call site).
        /// </summary>
        /// <typeparam name="TProject">The generated <c>Projects</c> metadata type (see Aspire's AppHost SDK) for the mock host project, e.g. <c>Projects.UnitTestEx_Aspire_MockHost</c>.</typeparam>
        /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
        /// <param name="name">The resource name (e.g. <c>"mockhost"</c>).</param>
        /// <param name="launchProfileName">The optional launch profile name to use; pass <c>null</c> (the default) to declare the endpoint explicitly via <c>WithHttpEndpoint</c>
        /// rather than relying on <c>launchSettings.json</c>.</param>
        /// <returns>The <see cref="IResourceBuilder{ProjectResource}"/> for the mock host, with its <c>http</c> endpoint bound to the <c>PORT</c> environment variable (see
        /// <see cref="UnitTestEx.Aspire.WireMockConsole.RunAsync"/>, which reads it); or <c>null</c> when not in run mode.</returns>
        public static IResourceBuilder<ProjectResource>? AddMockHostProject<TProject>(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? launchProfileName = null) where TProject : IProjectMetadata, new()
            => builder.ExecutionContext.IsRunMode ? builder.AddProject<TProject>(name, launchProfileName).WithHttpEndpoint(env: "PORT") : null;

        /// <summary>
        /// Sets an environment variable on <paramref name="builder"/> to <paramref name="source"/>'s named endpoint - but only when <paramref name="source"/> is not <c>null</c>.
        /// </summary>
        /// <remarks>This exists because <paramref name="source"/> (typically the result of <see cref="AddMockHostProject{TProject}"/>) is legitimately <c>null</c> in publish mode,
        /// yet Aspire's own <c>WithEnvironment(IResourceBuilder{T}, string, EndpointReference)</c> overload requires a non-null <see cref="EndpointReference"/> - and there is no
        /// null/default <see cref="IResourceBuilder{T}"/> to fall back on. A second overload differing from Aspire's only by a nullable <see cref="EndpointReference"/> annotation
        /// isn't possible either (both erase to the same CLR signature and would collide). Taking the nullable <paramref name="source"/> resource builder itself instead lets this
        /// no-op cleanly when it's <c>null</c>, so call sites stay a single fluent line instead of repeating an <c>if (source is not null)</c> guard (and risking someone forgetting
        /// it) at every optional-resource call site.</remarks>
        /// <typeparam name="TDestination">The resource type receiving the environment variable.</typeparam>
        /// <typeparam name="TSource">The resource type being referenced for its endpoint.</typeparam>
        /// <param name="builder">The <see cref="IResourceBuilder{TDestination}"/> to set the environment variable on.</param>
        /// <param name="name">The environment variable name.</param>
        /// <param name="source">The <see cref="IResourceBuilder{TSource}"/> to resolve the endpoint from, or <c>null</c> to no-op (e.g. when the resource does not exist in the
        /// current <see cref="DistributedApplicationExecutionContext"/>, such as publish mode).</param>
        /// <param name="endpointName">The endpoint name to resolve from <paramref name="source"/> (e.g. <c>"http"</c>).</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        public static IResourceBuilder<TDestination> WithMockHostEnvironment<TDestination, TSource>(this IResourceBuilder<TDestination> builder, string name, IResourceBuilder<TSource>? source, string endpointName)
            where TDestination : IResourceWithEnvironment where TSource : IResourceWithEndpoints
            => source is null ? builder : builder.WithEnvironment(name, source.GetEndpoint(endpointName));

        /// <summary>
        /// Waits for the named resource, within <paramref name="app"/>, to report a healthy status.
        /// </summary>
        /// <param name="app">The <see cref="DistributedApplication"/>.</param>
        /// <param name="resourceName">The resource name (as configured within the AppHost).</param>
        /// <param name="timeout">The timeout (defaults to <see cref="AspireTesterBase.DefaultWaitForResourceTimeout"/>); pass <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <remarks>Operates directly against <paramref name="app"/> (via <see cref="DistributedApplication.ResourceNotifications"/>) rather than a tester's own
        /// <see cref="AspireTesterBase.GetDistributedApplicationAsync"/>, so - unlike <see cref="AspireTesterBase.WaitForResourceAsync(string, TimeSpan?)"/> - it is safe to call from within
        /// a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/<see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/> callback (most usefully the latter, once every resource
        /// has actually been started/kicked off), as well as at any other time.</remarks>
        public static async Task WaitForResourceAsync(this DistributedApplication app, string resourceName, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentException.ThrowIfNullOrEmpty(resourceName);

            var effectiveTimeout = timeout ?? AspireTesterBase.DefaultWaitForResourceTimeout;
            var wait = app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName);
            await (effectiveTimeout == Timeout.InfiniteTimeSpan ? wait : wait.WaitAsync(effectiveTimeout)).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for all of the named resources, within <paramref name="app"/>, to report a healthy status, concurrently.
        /// </summary>
        /// <param name="app">The <see cref="DistributedApplication"/>.</param>
        /// <param name="resourceNames">The resource names (as configured within the AppHost).</param>
        /// <param name="timeout">The timeout (defaults to <see cref="AspireTesterBase.DefaultWaitForResourceTimeout"/>) applied to each resource independently; pass
        /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <remarks>Equivalent to awaiting <see cref="WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/> for each resource in parallel; if any resource fails to become
        /// healthy within the <paramref name="timeout"/>, the resulting exception is propagated once all waits have completed (or faulted). See
        /// <see cref="WaitForResourceAsync(DistributedApplication, string, TimeSpan?)"/> remarks regarding safe use from a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/> callback.</remarks>
        public static async Task WaitForResourceAsync(this DistributedApplication app, string[] resourceNames, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(resourceNames);

            await Task.WhenAll(resourceNames.Distinct().Select(rn => app.WaitForResourceAsync(rn, timeout))).ConfigureAwait(false);
        }

        /// <summary>
        /// Enables a fluent <see cref="AspireHttpMockClient"/> to stub HTTP responses from a real, out-of-process WireMock.Net server resource, directly against a started
        /// <paramref name="app"/> - i.e. without requiring an <see cref="AspireTesterBase"/>/Tier 2 test at all.
        /// </summary>
        /// <param name="app">The (already started - see <see cref="DistributedApplication.StartAsync"/>) <see cref="DistributedApplication"/>.</param>
        /// <param name="resourceName">The WireMock.Net server resource name (as configured within the AppHost) to target.</param>
        /// <param name="endpointName">The optional endpoint name; where not specified, the resource's default endpoint is used.</param>
        /// <param name="jsonComparerOptions">The optional <see cref="JsonElementComparerOptions"/> used by <see cref="AspireHttpMockRequest.WithJsonBodyUsingUnitTestExComparer(string, string[])"/>;
        /// defaults to a new <see cref="JsonElementComparerOptions"/> instance when not specified.</param>
        /// <returns>The <see cref="AspireHttpMockClient"/>.</returns>
        /// <remarks>Most useful for the AppHost project itself to pre-seed default request/response stubs - e.g. within its own entry point, after <c>await app.StartAsync()</c> - so an
        /// exploratory/manual run of the AppHost (outside of any UnitTestEx test) does not fail against an external dependency that has not yet been stubbed for that run; this reuses the
        /// exact same fluent syntax (<see cref="AspireHttpMockRequest.WithJsonBody(string, string[])"/>/<see cref="AspireHttpMockResponse"/> etc.) a Tier 2 test uses via
        /// <see cref="AspireTesterBase.HttpMock(string, string?)"/>. Equally usable from within a <see cref="AspireTesterBase{TAppHost, TSelf}.BeforeStart"/>/
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.AfterStart"/> callback (most usefully the latter, once resources actually exist), or any other code already holding a
        /// <see cref="DistributedApplication"/> instance directly. Unlike <see cref="AspireTesterBase.HttpMock(string, string?)"/>, <paramref name="jsonComparerOptions"/> is captured once
        /// at call time (there being no owning tester's <see cref="UnitTestEx.Abstractions.TesterBaseCore.JsonComparerOptions"/> to read live from).</remarks>
        public static AspireHttpMockClient HttpMock(this DistributedApplication app, string resourceName, string? endpointName = null, JsonElementComparerOptions? jsonComparerOptions = null)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentException.ThrowIfNullOrEmpty(resourceName);

            var options = jsonComparerOptions ?? new JsonElementComparerOptions();
            return new AspireHttpMockClient(async () =>
            {
                var httpClient = app.CreateHttpClient(resourceName, endpointName);
                return RestEase.RestClient.For<WireMock.Client.IWireMockAdminApi>(httpClient);
            }, () => options);
        }
    }
}
