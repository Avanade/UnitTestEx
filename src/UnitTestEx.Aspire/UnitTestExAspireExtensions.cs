// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting.ApplicationModel;

#pragma warning disable IDE0130 // Namespace does not match folder structure; by design.
namespace Aspire.Hosting
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Provides <see cref="IDistributedApplicationBuilder"/>/<see cref="IResourceBuilder{T}"/> extension methods for AppHost projects that use UnitTestEx's recommended
    /// self-hosted WireMock.Net "mockhost" pattern (see the README's "Aspire multi-host testing" section and <c>UnitTestEx.Aspire.MockHost</c>). Deliberately placed in the
    /// <see cref="Aspire.Hosting"/> namespace (matching Aspire's own extension method convention) so these methods are discoverable alongside <c>AddProject</c>/<c>WithEnvironment</c>
    /// without an extra <c>using</c>.
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
        public static IResourceBuilder<TDestination> WithMockHostEnvironment<TDestination, TSource>(
            this IResourceBuilder<TDestination> builder, string name, IResourceBuilder<TSource>? source, string endpointName)
            where TDestination : IResourceWithEnvironment
            where TSource : IResourceWithEndpoints
            => source is null ? builder : builder.WithEnvironment(name, source.GetEndpoint(endpointName));
    }
}
