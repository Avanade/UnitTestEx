// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx
{
    /// <summary>
    /// Represents an <b>API</b> error being <see cref="Field"/> and <see cref="Message"/>.
    /// </summary>
    public class ApiError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiError"/> class.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <param name="message">The error message.</param>
        public ApiError(string? field, string message)
        {
            Field = field;
            Message = message;
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        public string? Field { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }
    }
}