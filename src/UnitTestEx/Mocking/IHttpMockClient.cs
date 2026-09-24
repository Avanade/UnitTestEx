// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Net.Http;

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
    }
}
