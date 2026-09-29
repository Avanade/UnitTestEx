// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents the result of adding a body matcher to an <see cref="IHttpMockRequest"/> and to <see cref="Respond"/> accordingly.
    /// </summary>
    /// <remarks>Common to both Tier 1's <see cref="MockHttpClientRequestBody"/> and Tier 2/3's <c>AspireHttpMockRequestBody</c>.</remarks>
    public interface IHttpMockRequestBody
    {
        /// <summary>
        /// Gets the <see cref="IHttpMockResponse"/> to configure the stubbed response.
        /// </summary>
        IHttpMockResponse Respond { get; }
    }
}
