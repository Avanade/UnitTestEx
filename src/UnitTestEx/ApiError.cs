// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        /// <summary>
        /// Indicates whether the message has been matched.
        /// </summary>
        internal bool IsMatched { get; set; }

        /// <summary>
        /// Trys to match the <paramref name="expected"/> and <paramref name="actual"/> <see cref="ApiError"/> contents.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <param name="actual">The actual errors.</param>
        /// <param name="includeField">Indicates whether to include the <see cref="ApiError.Field"/> in the matching.</param>
        /// <param name="message">The output match message.</param>
        /// <returns><c>true</c> where matched; otherwise, <c>false</c>.</returns>
        public static bool TryAreMatched(IEnumerable<ApiError> expected, IEnumerable<ApiError> actual, bool includeField, out string? message)
        {
            var expErr = false;
            var actErr = false;
            var sb = new StringBuilder();

            foreach (var ear in expected)
            {
                var aar = actual.Where(x => ((includeField && x.Field == ear.Field) || !includeField) && x.Message == ear.Message && !x.IsMatched).FirstOrDefault();
                if (aar == null)
                {
                    if (!expErr)
                    {
                        expErr = true;
                        sb.AppendLine("Expected error(s) not matched:");
                    }

                    sb.AppendLine($"  {ear.Message}{(includeField ? $" [{ear.Field ?? "null"}]" : "")}");
                }
                else
                    aar.IsMatched = true;
            }

            foreach (var aar in actual.Where(x => !x.IsMatched))
            {
                if (!actErr)
                {
                    actErr = true;
                    sb.AppendLine("Actual error(s) not matched:");
                }

                sb.AppendLine($"  {aar.Message}{(includeField ? $" [{aar.Field ?? "null"}]" : "")}");
            }

            message = sb.Length > 0 ? sb.ToString() : null;
            return sb.Length == 0;
        }
    }
}