// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Mocking;
using WireMock.Client;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Represents a stubbed mapping (or, for a <see cref="AspireHttpMockResponse.WithSequenceAsync">sequence</see>, the set of mappings) that has been applied (posted) to a
    /// WireMock.Net server resource, allowing its invocation to be verified after the fact.
    /// </summary>
    /// <remarks>Implements the shared <see cref="IHttpMockedRequest"/> abstraction (see that type's remarks) so that test-authoring code can be written once against the interface
    /// and reused identically regardless of which tier applied it.</remarks>
    public sealed class AspireHttpMockedRequest : IHttpMockedRequest
    {
        private readonly IWireMockAdminApi _adminApi;
        private readonly IReadOnlyList<Guid> _mappingIds;
        private readonly Times? _expectedTimes;
        private readonly Guid? _exhaustedMappingId;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockedRequest"/> class for a single (non-sequence) mapping.
        /// </summary>
        /// <param name="adminApi">The <see cref="IWireMockAdminApi"/>.</param>
        /// <param name="mappingIds">The unique identifier(s) assigned to the posted mapping(s).</param>
        /// <param name="expectedTimes">The number of times the request is expected to be invoked (see <see cref="AspireHttpMockRequest.Times(Times)"/>); defaults to
        /// <see cref="Times.AtLeastOnce()"/> where not specified.</param>
        internal AspireHttpMockedRequest(IWireMockAdminApi adminApi, IReadOnlyList<Guid> mappingIds, Times? expectedTimes)
        {
            _adminApi = adminApi;
            _mappingIds = mappingIds;
            _expectedTimes = expectedTimes;
            MappingId = mappingIds[0];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockedRequest"/> class for a <see cref="AspireHttpMockResponse.WithSequenceAsync">sequence</see> of mappings.
        /// </summary>
        private AspireHttpMockedRequest(IWireMockAdminApi adminApi, IReadOnlyList<Guid> responseMappingIds, Guid exhaustedMappingId)
        {
            _adminApi = adminApi;
            _mappingIds = responseMappingIds;
            _exhaustedMappingId = exhaustedMappingId;
            MappingId = responseMappingIds[0];
        }

        /// <summary>
        /// Creates a new <see cref="AspireHttpMockedRequest"/> for a <see cref="AspireHttpMockResponse.WithSequenceAsync">sequence</see> of mappings.
        /// </summary>
        /// <param name="adminApi">The <see cref="IWireMockAdminApi"/>.</param>
        /// <param name="responseMappingIds">The unique identifiers assigned to each of the sequence's posted per-response mappings (excluding the "exhausted" guard mapping).</param>
        /// <param name="exhaustedMappingId">The unique identifier assigned to the sequence's "exhausted" guard mapping.</param>
        /// <returns>The <see cref="AspireHttpMockedRequest"/>.</returns>
        internal static AspireHttpMockedRequest ForSequence(IWireMockAdminApi adminApi, IReadOnlyList<Guid> responseMappingIds, Guid exhaustedMappingId) => new(adminApi, responseMappingIds, exhaustedMappingId);

        /// <summary>
        /// Gets the unique identifier assigned to the (first) posted mapping; used to correlate matched requests via <see cref="VerifyAsync(CancellationToken)"/>.
        /// </summary>
        /// <remarks>Where applied via <see cref="AspireHttpMockResponse.WithSequenceAsync"/>, this is the identifier of the sequence's first mapping only; see <see cref="MappingIds"/>
        /// for the complete set.</remarks>
        public Guid MappingId { get; }

        /// <summary>
        /// Gets the complete set of unique identifiers assigned to the posted per-response mapping(s); a single entry unless applied via <see cref="AspireHttpMockResponse.WithSequenceAsync"/>
        /// (in which case the sequence's internal "exhausted" guard mapping is not included).
        /// </summary>
        public IReadOnlyList<Guid> MappingIds => _mappingIds;

        /// <summary>
        /// Verifies the stubbed request was invoked the expected number of times.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="MockHttpClientException">Thrown when the actual invocation count does not satisfy the expectation.</exception>
        /// <remarks>Where applied via <see cref="AspireHttpMockRequest.Times(Times)"/>, verifies the total invocation count (in total, across all <see cref="MappingIds"/>) satisfies the
        /// configured <see cref="Times"/> (or at least once where not configured). Where applied via <see cref="AspireHttpMockResponse.WithSequenceAsync"/>, verifies that each configured
        /// response was invoked exactly once - no more, no less - mirroring Tier 1's <see cref="Mocking.MockHttpClientResponse.WithSequence"/>; any invocation beyond the configured responses
        /// (which will have already received a distinct <see cref="System.Net.HttpStatusCode.InternalServerError"/> "exhausted" response) is reported here as a failure too.</remarks>
        public async Task VerifyAsync(CancellationToken cancellationToken = default)
        {
            if (_exhaustedMappingId.HasValue)
            {
                await VerifySequenceAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var count = 0;
            foreach (var mappingId in _mappingIds)
            {
                var requests = await _adminApi.FindRequestsByMappingGuidAsync(mappingId, cancellationToken).ConfigureAwait(false);
                count += requests?.Count ?? 0;
            }

            var expected = _expectedTimes ?? Times.AtLeastOnce();
            expected.Deconstruct(out var from, out var to);
            if (count < from || count > to)
                throw new MockHttpClientException($"The request was invoked {count} times; expected {expected}. Mapping(s): {string.Join(", ", _mappingIds.Select(m => m.ToString()))}.");
        }

        /// <summary>
        /// Verifies a <see cref="AspireHttpMockResponse.WithSequenceAsync">sequence</see>'s invocations: exactly one per configured response, and none against the "exhausted" guard.
        /// </summary>
        private async Task VerifySequenceAsync(CancellationToken cancellationToken)
        {
            var exhaustedRequests = await _adminApi.FindRequestsByMappingGuidAsync(_exhaustedMappingId!.Value, cancellationToken).ConfigureAwait(false);
            var exhaustedCount = exhaustedRequests?.Count ?? 0;
            if (exhaustedCount > 0)
                throw new MockHttpClientException($"There were {_mappingIds.Count} response(s) configured for the sequence and these have been exhausted; i.e. {exhaustedCount} unexpected additional invocation(s) occurred. Mapping: {_exhaustedMappingId}.");

            var count = 0;
            foreach (var mappingId in _mappingIds)
            {
                var requests = await _adminApi.FindRequestsByMappingGuidAsync(mappingId, cancellationToken).ConfigureAwait(false);
                count += requests?.Count ?? 0;
            }

            if (count != _mappingIds.Count)
                throw new MockHttpClientException($"There were {_mappingIds.Count} response(s) configured for the sequence and only {count} response(s) invoked. Mapping(s): {string.Join(", ", _mappingIds.Select(m => m.ToString()))}.");
        }
    }
}
