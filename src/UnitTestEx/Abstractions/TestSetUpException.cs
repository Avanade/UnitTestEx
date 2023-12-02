// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Represents a <see cref="TestSetUp"/> exception.
    /// </summary>
    /// <param name="message">The message text.</param>
    public class TestSetUpException(string? message = null) : Exception($"The test set up was unsuccessful:{Environment.NewLine}{message ?? "No reason specified."}") { }
}