// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using UnitTestEx.Mocking;

namespace UnitTestEx.Xunit.Test
{
    /// <summary>
    /// A single, tier-agnostic HTTP mock configuration method - written once against the shared <see cref="IHttpMockClient"/> abstraction - used to prove that Tier 1's in-process
    /// <see cref="MockHttpClient"/> and Tier 2/3's out-of-process <c>AspireHttpMockClient</c> are interchangeable for basic request/response configuration.
    /// </summary>
    /// <remarks>Deliberately duplicated verbatim in both the <c>UnitTestEx.Xunit.Test</c> and <c>UnitTestEx.Aspire.Xunit.Test</c> projects (rather than factored into a shared project
    /// reference) to keep this proof-of-concept minimal and avoid introducing a new cross-project dependency purely for a demonstration/test method.</remarks>
    internal static class HttpMockSharedConfig
    {
        /// <summary>
        /// Configures a stub for a GET request to <paramref name="requestUri"/> that returns a fixed product payload, using only the shared <see cref="IHttpMockClient"/> abstraction.
        /// </summary>
        /// <param name="client">The <see cref="IHttpMockClient"/> - either tier's concrete client, accessed purely through the shared interface.</param>
        /// <param name="requestUri">The request URI to match (tier-specific path convention, e.g. with or without a leading slash).</param>
        /// <returns>The <see cref="IHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        internal static async Task<IHttpMockedRequest> ConfigureProductStubAsync(IHttpMockClient client, string requestUri) =>
            await client.Request(HttpMethod.Get, requestUri)
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithJsonAsync(new { id = "Shared", description = "Configured via the shared IHttpMockClient interface." });
    }
}
