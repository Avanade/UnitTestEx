// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx
{
    /// <summary>
    /// Represents an <b>API-style</b> error being <see cref="Field"/> and <see cref="Message"/>.
    /// </summary>
    /// <param name="field">The optional field/property name.</param>
    /// <param name="message">The error message.</param>
    public class ApiError(string? field, string message)
    {
        /// <summary>
        /// Gets the optional field/property name.
        /// </summary>
        public string? Field { get; } = field;

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; } = message;
    }
}