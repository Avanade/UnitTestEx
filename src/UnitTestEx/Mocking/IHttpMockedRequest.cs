// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents a stubbed request/response mapping that has been applied and can be verified after the fact.
    /// </summary>
    /// <remarks>Common to both the in-process, Moq-based <see cref="MockHttpClientRequest"/> (Tier 1) and the out-of-process, WireMock.Net-based <c>AspireHttpMockedRequest</c> (Tier 2/3),
    /// allowing shared test-authoring code to verify a stubbed mapping regardless of which tier applied it.</remarks>
    public interface IHttpMockedRequest
    {
        /// <summary>
        /// Verifies the stubbed request was invoked the expected number of times.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="MockHttpClientException">Thrown when the actual invocation count does not satisfy the expectation.</exception>
        Task VerifyAsync(CancellationToken cancellationToken = default);
    }
}
