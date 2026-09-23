// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Represents the result of adding a body matcher to the <see cref="AspireHttpMockRequest"/> and to <see cref="Respond"/> accordingly.
    /// </summary>
    public sealed class AspireHttpMockRequestBody
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockRequestBody"/> class.
        /// </summary>
        /// <param name="request">The <see cref="AspireHttpMockRequest"/>.</param>
        internal AspireHttpMockRequestBody(AspireHttpMockRequest request) => Request = request;

        /// <summary>
        /// Gets the owning <see cref="AspireHttpMockRequest"/>.
        /// </summary>
        internal AspireHttpMockRequest Request { get; }

        /// <summary>
        /// Gets the <see cref="AspireHttpMockResponse"/> to configure the stubbed response.
        /// </summary>
        public AspireHttpMockResponse Respond => new(Request);
    }
}
