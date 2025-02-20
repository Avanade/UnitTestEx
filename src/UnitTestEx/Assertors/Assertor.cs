// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Provides assertion helper methods.
    /// </summary>
    public static class Assertor
    {
        /// <summary>
        /// Converts the <paramref name="errors"/> dictionary to an <see cref="ApiError"/> array.
        /// </summary>
        /// <param name="errors">The errors.</param>
        /// <returns>The <see cref="ApiError"/> array.</returns>
        public static ApiError[] ConvertToApiErrors(IDictionary<string, string[]>? errors)
        {
            List<ApiError>? actual = [];

            if (errors != null)
            {
                foreach (var kvp in errors.Where(x => !string.IsNullOrEmpty(x.Key)))
                {
                    foreach (var message in kvp.Value.Where(x => !string.IsNullOrEmpty(x)))
                    {
                        (actual ??= []).Add(new ApiError(kvp.Key, message));
                    }
                }
            }

            return actual?.ToArray() ?? [];
        }

        /// <summary>
        /// Trys to match the <paramref name="expected"/> and <paramref name="actual"/> <see cref="ApiError"/> contents.
        /// </summary>
        /// <param name="expected">The expected <see cref="ApiError"/> list.</param>
        /// <param name="actual">The actual <see cref="ApiError"/> list.</param>
        /// <param name="errorMessage">The error message where they do not match.</param>
        /// <returns><c>true</c> where matched; otherwise, <c>false</c>.</returns>
        public static bool TryAreErrorsMatched(IEnumerable<ApiError>? expected, IEnumerable<ApiError>? actual, out string? errorMessage)
        {
            var exp = (from e in (expected ?? Array.Empty<ApiError>())
                       where !(actual ?? Array.Empty<ApiError>()).Any(a => a.Message == e.Message && (e.Field == null || a.Field == e.Field))
                       select e).ToList();

            var act = (from a in (actual ?? Array.Empty<ApiError>())
                       where !(expected ?? Array.Empty<ApiError>()).Any(e => a.Message == e.Message && (e.Field == null || a.Field == e.Field))
                       select a).ToList();

            var sb = new StringBuilder();
            if (exp.Count > 0)
            {
                sb.AppendLine(" Expected messages not matched:");
                exp.ForEach(m => sb.AppendLine($" Error: {m.Message} {(m.Field != null ? $"[{m.Field}]" : null)}"));
            }

            if (act.Count > 0)
            {
                sb.AppendLine(" Actual messages not matched:");
                act.ForEach(m => sb.AppendLine($" Error: {m.Message} {(m.Field != null ? $"[{m.Field}]" : null)}"));
            }

            errorMessage = sb.Length > 0 ? $"Error messages mismatch:{System.Environment.NewLine}{sb}" : null;
            return sb.Length == 0;
        }
    }
}