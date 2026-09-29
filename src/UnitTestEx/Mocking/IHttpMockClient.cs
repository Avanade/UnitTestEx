// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides <see cref="HttpClient"/> mocking, common to both Tier 1's in-process <see cref="MockHttpClient"/> (a Moq-based <see cref="HttpMessageHandler"/> substitution) and
    /// Tier 2/3's out-of-process <c>AspireHttpMockClient</c> (a thin wrapper over a real WireMock.Net server resource).
    /// </summary>
    /// <remarks>This abstraction allows a single piece of test-authoring code (e.g. a shared <c>static</c> helper method) to configure request/response stubbing identically regardless
    /// of which tier is under test - only the concrete <see cref="IHttpMockClient"/> instance passed in differs. The tier-specific semantics that remain (JSON comparison engine,
    /// sequence-exhaustion behaviour) despite the shared surface are documented on each concrete implementation.</remarks>
    public interface IHttpMockClient
    {
        /// <summary>
        /// Begins configuration of a stubbed request/response mapping.
        /// </summary>
        /// <param name="method">The <see cref="HttpMethod"/> to match; where not specified any method will match.</param>
        /// <param name="requestUri">The relative request URI (path) to match; where not specified any path will match.</param>
        /// <returns>The <see cref="IHttpMockRequest"/> to continue the fluent-style configuration.</returns>
        IHttpMockRequest Request(HttpMethod? method = null, string? requestUri = null);

        /// <summary>
        /// Adds mocked request(s) from the named embedded resource (formatted as either YAML or JSON) within the calling <see cref="Assembly"/>, using the same schema as Tier 1's native
        /// <see cref="MockHttpClient.WithRequestsFromResource{TAssembly}(string)"/>.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="System.Type"/> used to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        Task WithRequestsFromResourceAsync<TAssembly>(string resourceName, CancellationToken cancellationToken = default) => WithRequestsFromResourceAsync(resourceName, typeof(TAssembly).Assembly, cancellationToken);

        /// <summary>
        /// Adds mocked request(s) from the named embedded resource (formatted as either YAML or JSON), using the same schema as Tier 1's native <see cref="MockHttpClient.WithRequestsFromResource(string, Assembly?)"/>.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to the calling <see cref="Assembly"/> where not specified.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <remarks>Works identically regardless of which tier's <see cref="IHttpMockClient"/> is supplied, composed purely from this interface's other members - see <see cref="HttpMockResourceConfig"/>
        /// for the shared implementation, including the one intentional behavioral difference from Tier 1's native method (how a request entry that omits <c>body</c> is matched).</remarks>
        Task WithRequestsFromResourceAsync(string resourceName, Assembly? assembly = null, CancellationToken cancellationToken = default)
            => HttpMockResourceConfig.AddRequestsFromResourceAsync(this, resourceName, assembly ?? Assembly.GetCallingAssembly(), cancellationToken);
    }
}
