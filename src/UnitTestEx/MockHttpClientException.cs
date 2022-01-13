// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx
{
    /// <summary>
    /// Represents a <see cref="Mocking.MockHttpClient"/> runtime exception.
    /// </summary>
    public class MockHttpClientException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientException"/> class.
        /// </summary>
        public MockHttpClientException() : this(null!) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientException"/> class with a specified messsage.
        /// </summary>
        /// <param name="message">The message text.</param>
        public MockHttpClientException(string? message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientException"/> class with a specified messsage and inner exception.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        public MockHttpClientException(string? message, Exception innerException) : base(message, innerException) { }
    }
}