// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents a <see cref="MockHttpClientRequest"/> rule containing the <see cref="Response"/>.
    /// </summary>
    public class MockHttpClientRequestRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientRequestRule"/> class.
        /// </summary>
        internal MockHttpClientRequestRule() { }

        /// <summary>
        /// Gets or sets the <see cref="MockHttpClientRequestBody"/>.
        /// </summary>
        public MockHttpClientRequestBody? Body { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        public MockHttpClientResponse? Response { get; set; }
    }
}