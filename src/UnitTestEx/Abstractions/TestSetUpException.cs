// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Represents a <see cref="TestSetUp"/> exception.
    /// </summary>
    public class TestSetUpException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestSetUpException"/> class.
        /// </summary>
        /// <param name="message">The message text.</param>
        public TestSetUpException(string? message = null) : base($"The test set up was unsuccessful:{Environment.NewLine}{message ?? "No reason specified."}") { }
    }
}