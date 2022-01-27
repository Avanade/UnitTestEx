// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System.Collections.Generic;

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents a <see cref="MockHttpClientRequest"/> rule for managing the <see cref="Responses"/>.
    /// </summary>
    internal class MockHttpClientRequestRule
    {
        /// <summary>
        /// Gets or sets the <see cref="MockHttpClientRequestBody"/>.
        /// </summary>
        internal MockHttpClientRequestBody? Body { get; set; }

        /// <summary>
        /// Gets or sets the primary <see cref="MockHttpClientRequestBody"/>.
        /// </summary>
        internal MockHttpClientResponse? Response { get; set; }

        /// <summary>
        /// Gets the <see cref="MockHttpClientResponse"/> sequence collection.
        /// </summary>
        internal List<MockHttpClientResponse>? Responses { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Moq.Times"/>.
        /// </summary>
        internal Times? Times;
    }
}