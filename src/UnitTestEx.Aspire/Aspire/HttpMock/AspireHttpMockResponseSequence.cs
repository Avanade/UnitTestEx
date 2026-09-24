// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Collections.Generic;
using UnitTestEx.Mocking;
using WireMock.Admin.Mappings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Provides the means to build up a sequence of responses via <see cref="AspireHttpMockResponse.WithSequenceAsync"/>.
    /// </summary>
    /// <remarks>Mirrors Tier 1's <see cref="Mocking.MockHttpClientResponseSequence"/>; unlike Tier 1, the actual posting of the underlying mappings occurs only after the sequence
    /// configuration action has completed (see <see cref="AspireHttpMockResponse.WithSequenceAsync"/>), as each entry requires a real HTTP POST to the WireMock.Net server resource's
    /// admin API - configuring the sequence itself remains synchronous.
    /// <para>Implements the shared <see cref="IHttpMockResponseSequence"/> abstraction (see that type's remarks) so that test-authoring code can be written once against the interface
    /// and reused identically regardless of which tier applied it.</para></remarks>
    public sealed class AspireHttpMockResponseSequence : IHttpMockResponseSequence
    {
        private readonly List<ResponseModel> _responses = [];

        /// <summary>
        /// Gets the <see cref="ResponseModel"/>s added via <see cref="Respond"/>, in sequence order.
        /// </summary>
        internal IReadOnlyList<ResponseModel> Responses => _responses;

        /// <summary>
        /// Adds the next <see cref="AspireHttpMockResponseSequenceItem"/> in sequence.
        /// </summary>
        /// <returns>The next <see cref="AspireHttpMockResponseSequenceItem"/> in sequence, to configure the corresponding response.</returns>
        public AspireHttpMockResponseSequenceItem Respond()
        {
            var response = new ResponseModel();
            _responses.Add(response);
            return new AspireHttpMockResponseSequenceItem(response);
        }

        /// <inheritdoc/>
        IHttpMockResponseSequenceItem IHttpMockResponseSequence.Respond() => Respond();
    }
}
