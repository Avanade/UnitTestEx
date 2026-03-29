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

        /// <summary>
        /// Implicitly converts a <c>(string? field, string message)</c> tuple to an <see cref="ApiError"/>.
        /// </summary>
        /// <param name="error">The tuple containing the field and message.</param>
        public static implicit operator ApiError((string? field, string message) error) => new(error.field, error.message);

        /// <summary>
        /// Implicitly converts a <paramref name="message"/> <see cref="string"/> to an <see cref="ApiError"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        public static implicit operator ApiError(string message) => new(null, message);
    }
}