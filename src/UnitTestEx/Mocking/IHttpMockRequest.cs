// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Reflection;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents the request-matching configuration for a stubbed <see cref="IHttpMockClient"/> mapping.
    /// </summary>
    /// <remarks>Common to both Tier 1's <see cref="MockHttpClientRequest"/> and Tier 2/3's <c>AspireHttpMockRequest</c>, enabling shared test-authoring code (e.g. a helper method
    /// accepting an <see cref="IHttpMockClient"/>) to configure request matching identically regardless of which tier applied it.
    /// <para>This is deliberately a minimal, irreducible core of abstract members - the remaining convenience overloads (e.g. a plain-text <c>WithBody(text)</c>, or the JSON-embedded-resource
    /// variants) are provided as C# default interface methods, composed purely from the core members below, so concrete implementations (i.e. each tier) don't need to reimplement them.</para></remarks>
    public interface IHttpMockRequest
    {
        /// <summary>
        /// Sets the number of <paramref name="times"/> that the request is expected to be invoked.
        /// </summary>
        /// <param name="times">The expected <see cref="Moq.Times"/>.</param>
        /// <returns>The <see cref="IHttpMockRequest"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Where not set, verification will default to at least one invocation (see <see cref="Moq.Times.AtLeastOnce()"/>). Each time this is invoked it will override the
        /// previously set value.</remarks>
        IHttpMockRequest Times(Times times);

        /// <summary>
        /// Indicates that the request will match regardless of the body content.
        /// </summary>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequestBody WithAnyBody();

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="body"/> exactly, with the specified <paramref name="mediaType"/>.
        /// </summary>
        /// <param name="body">The exact body text to match.</param>
        /// <param name="mediaType">The media type of the request.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequestBody WithBody(string body, string mediaType);

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="json"/> content.
        /// </summary>
        /// <param name="json">The JSON body to match.</param>
        /// <param name="pathsToIgnore">The paths to ignore from the comparison.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        /// <remarks>The exact JSON comparison semantics (including how <paramref name="pathsToIgnore"/> is interpreted) are tier-specific - see the native <c>WithJsonBody</c> documentation
        /// on the concrete implementation being used; they are <b>not</b> guaranteed to be equivalent between tiers.</remarks>
#if NET7_0_OR_GREATER
        IHttpMockRequestBody WithJsonBody([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore);
#else
        IHttpMockRequestBody WithJsonBody(string json, params string[] pathsToIgnore);
#endif

        /// <summary>
        /// Indicates that the request body must match the JSON serialized representation of the specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize and match.</param>
        /// <param name="pathsToIgnore">The paths to ignore from the comparison.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        /// <remarks>Kept as a core (non-default) member, rather than composed from <see cref="WithJsonBody(string, string[])"/>, because Tier 1 serializes <paramref name="value"/> using
        /// its own configurable <c>IJsonSerializer</c> (which may not be <c>System.Text.Json</c>); a shared default implementation could not honour that. See the
        /// <see cref="WithJsonBody(string, string[])"/> remarks regarding tier-specific JSON comparison semantics.</remarks>
        IHttpMockRequestBody WithJsonBody<T>(T value, params string[] pathsToIgnore);

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="text"/> exactly, with a <see cref="MediaTypeNames.Text.Plain"/> media type.
        /// </summary>
        /// <param name="text">The exact body text to match.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequestBody WithBody(string text) => WithBody(text, MediaTypeNames.Text.Plain);

        /// <summary>
        /// Indicates that the request body must match the JSON content of the named embedded resource within the calling <see cref="Assembly"/>.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="pathsToIgnore">The paths to ignore from the comparison.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequestBody WithJsonResourceBody<TAssembly>(string resourceName, params string[] pathsToIgnore) => WithJsonResourceBody(resourceName, typeof(TAssembly).Assembly, pathsToIgnore);

        /// <summary>
        /// Indicates that the request body must match the JSON content of the named embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to the calling <see cref="Assembly"/> where not specified.</param>
        /// <param name="pathsToIgnore">The paths to ignore from the comparison.</param>
        /// <returns>The <see cref="IHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequestBody WithJsonResourceBody(string resourceName, Assembly? assembly = null, params string[] pathsToIgnore) => WithJsonBody(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), pathsToIgnore);
    }
}
