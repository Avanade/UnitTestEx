// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Provides the means to build up a sequence of responses via <see cref="IHttpMockResponse.WithSequenceAsync"/>.
    /// </summary>
    /// <remarks>Common to both Tier 1's <see cref="MockHttpClientResponseSequence"/> and Tier 2/3's <c>AspireHttpMockResponseSequence</c>.</remarks>
    public interface IHttpMockResponseSequence
    {
        /// <summary>
        /// Adds the next <see cref="IHttpMockResponseSequenceItem"/> in sequence.
        /// </summary>
        /// <returns>The next <see cref="IHttpMockResponseSequenceItem"/> in sequence, to configure the corresponding response.</returns>
        IHttpMockResponseSequenceItem Respond();
    }
}
