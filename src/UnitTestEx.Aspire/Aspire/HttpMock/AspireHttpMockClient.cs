// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using UnitTestEx.Mocking;
using WireMock.Admin.Mappings;
using WireMock.Client;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Provides a thin fluent wrapper over a real, out-of-process WireMock.Net server resource (added to the AppHost via the official <c>WireMock.Net.Aspire</c> package's
    /// <c>builder.AddWireMock(name)</c>), for stubbing HTTP responses from a Tier 2 (<see cref="AspireTesterBase{TAppHost, TSelf}"/>) multi-host test.
    /// </summary>
    /// <remarks>Unlike Tier 1's in-memory, purely synchronous <see cref="Mocking.MockHttpClient"/> (a Moq-based <see cref="HttpMessageHandler"/> substitution), this issues genuine HTTP requests
    /// to the WireMock.Net server's admin API (a separate OS process, potentially in a container); every stub-defining and verification method is therefore asynchronous - there is no honest way
    /// to hide that real network I/O behind a synchronous-looking API.
    /// <para>Implements the shared <see cref="IHttpMockClient"/> abstraction (see that type's remarks) so that test-authoring code can be written once against the interface and reused
    /// identically regardless of which tier applied it.</para></remarks>
    public sealed class AspireHttpMockClient : IHttpMockClient
    {
        private readonly Func<Task<IWireMockAdminApi>> _adminApiFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockClient"/> class.
        /// </summary>
        /// <param name="adminApiFactory">The factory function used to (asynchronously) resolve the underlying <see cref="IWireMockAdminApi"/>.</param>
        internal AspireHttpMockClient(Func<Task<IWireMockAdminApi>> adminApiFactory) => _adminApiFactory = adminApiFactory ?? throw new ArgumentNullException(nameof(adminApiFactory));

        /// <summary>
        /// Gets the underlying <see cref="IWireMockAdminApi"/> as an escape hatch for scenarios not covered by the fluent wrapper (e.g. scenarios, proxying, gRPC/protobuf mappings).
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IWireMockAdminApi"/>.</returns>
        public Task<IWireMockAdminApi> GetAdminApiAsync(CancellationToken cancellationToken = default) => _adminApiFactory();

        /// <summary>
        /// Begins configuration of a stubbed request/response mapping.
        /// </summary>
        /// <param name="method">The <see cref="HttpMethod"/> to match; where not specified any method will match.</param>
        /// <param name="requestUri">The relative request URI (path) to match (exact match); where not specified any path will match.</param>
        /// <returns>The <see cref="AspireHttpMockRequest"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequest Request(HttpMethod? method = null, string? requestUri = null) => new(this, method, requestUri);

        /// <inheritdoc/>
        IHttpMockRequest IHttpMockClient.Request(HttpMethod? method, string? requestUri) => Request(method, requestUri);

        /// <summary>
        /// Removes <b>all</b> previously configured mappings and clears the recorded request log on the underlying WireMock.Net server resource.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <remarks>As the underlying <see cref="global::Aspire.Hosting.DistributedApplication"/> is expensive to rebuild (see <see cref="AspireTesterBase{TAppHost, TSelf}.OnResetHost"/>), the mapped
        /// server resource's state otherwise persists across all tests sharing the same host instance; call this (e.g. from a per-test setup) to isolate each test's stubs.</remarks>
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            var adminApi = await GetAdminApiAsync(cancellationToken).ConfigureAwait(false);
            _ = await adminApi.ResetMappingsAsync(null, cancellationToken).ConfigureAwait(false);
            _ = await adminApi.ResetRequestsAsync(cancellationToken).ConfigureAwait(false);
            _ = await adminApi.ResetScenariosAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Posts the specified <paramref name="mapping"/> to the underlying WireMock.Net server resource's admin API.
        /// </summary>
        /// <param name="mapping">The <see cref="MappingModel"/> to post.</param>
        /// <param name="expectedTimes">The number of times the request is expected to be invoked; used as the default for <see cref="AspireHttpMockedRequest.VerifyAsync"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The resulting <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation after the fact.</returns>
        internal async Task<AspireHttpMockedRequest> ApplyAsync(MappingModel mapping, Times? expectedTimes, CancellationToken cancellationToken)
        {
            mapping.Guid ??= Guid.NewGuid();

            var adminApi = await GetAdminApiAsync(cancellationToken).ConfigureAwait(false);
            _ = await adminApi.PostMappingAsync(mapping, cancellationToken).ConfigureAwait(false);

            return new AspireHttpMockedRequest(adminApi, [mapping.Guid.Value], expectedTimes);
        }

        /// <summary>
        /// Posts the specified sequence of <paramref name="mappings"/> (chained together via a shared WireMock.Net Scenario/state) to the underlying WireMock.Net server resource's admin API.
        /// </summary>
        /// <param name="mappings">The ordered <see cref="MappingModel"/>s to post; all but the last are one per response in the sequence, with the last being the "exhausted" guard mapping
        /// (see <see cref="AspireHttpMockResponse.WithSequenceAsync"/>).</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The resulting <see cref="AspireHttpMockedRequest"/> which can be used to verify invocation (exactly once per configured response) after the fact.</returns>
        internal async Task<AspireHttpMockedRequest> ApplySequenceAsync(IReadOnlyList<MappingModel> mappings, CancellationToken cancellationToken)
        {
            var adminApi = await GetAdminApiAsync(cancellationToken).ConfigureAwait(false);
            var mappingIds = new List<Guid>(mappings.Count);

            foreach (var mapping in mappings)
            {
                mapping.Guid ??= Guid.NewGuid();
                mappingIds.Add(mapping.Guid.Value);
                _ = await adminApi.PostMappingAsync(mapping, cancellationToken).ConfigureAwait(false);
            }

            // The last posted mapping is the "exhausted" guard; the rest are the actual per-response mappings.
            var exhaustedMappingId = mappingIds[^1];
            var responseMappingIds = mappingIds.GetRange(0, mappingIds.Count - 1);

            return AspireHttpMockedRequest.ForSequence(adminApi, responseMappingIds, exhaustedMappingId);
        }
    }
}
