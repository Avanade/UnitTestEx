// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Represents the result of a <see cref="JsonElementComparer"/>.
    /// </summary>
    public sealed class JsonElementComparerResult
    {
        private List<JsonElementDifference>? _differences;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonElementComparerResult"/> class.
        /// </summary>
        /// <param name="left">The left <see cref="JsonElement"/>.</param>
        /// <param name="right">The right <see cref="JsonElement"/>.</param>
        /// <param name="maxDifferences">The maximum number of differences detect.</param>
        internal JsonElementComparerResult(JsonElement left, JsonElement right, int maxDifferences)
        {
            Left = left;
            Right = right;
            MaxDifferences = maxDifferences;
        }

        /// <summary>
        /// Gets the left <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement Left { get; }

        /// <summary>
        /// Gets the right <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement Right { get; }

        /// <summary>
        /// Gets the maximum number of differences to detect.
        /// </summary>
        public int MaxDifferences { get; }

        /// <summary>
        /// Indicates whether the two JSON elements are considered equal.
        /// </summary>
        public bool AreEqual => DifferenceCount == 0;

        /// <summary>
        /// Indicates whether there are any differences between the two JSON elements based on the specified criteria.
        /// </summary>
        public bool HasDifferences => DifferenceCount != 0;

        /// <summary>
        /// Gets the current number of differences detected.
        /// </summary>
        public int DifferenceCount => _differences?.Count ?? 0;

        /// <summary>
        /// Indicates whether the maximum number of differences specified to detect has been found.
        /// </summary>
        public bool IsMaxDifferencesFound => DifferenceCount >= MaxDifferences;

        /// <summary>
        /// Indicates whether to always replace all array items (where at least one item has changed).
        /// </summary>
        /// <remarks>The formal specification <see href="https://tools.ietf.org/html/rfc7396"/> explictly states that an <see cref="JsonValueKind.Array"/> is to be a replacement operation.</remarks>
        public bool AlwaysReplaceAllArrayItems { get; }

        /// <summary>
        /// Gets the <see cref="JsonElementDifference"/> array.
        /// </summary>
        /// <remarks>The differences found up to the <see cref="MaxDifferences"/> specified.</remarks>
        public JsonElementDifference[] GetDifferences() => _differences is null ? [] : _differences.ToArray();

        /// <summary>
        /// Adds a <see cref="JsonElementDifference"/>.
        /// </summary>
        /// <param name="difference">The <see cref="JsonElementDifference"/>.</param>
        internal void AddDifference(JsonElementDifference difference) => (_differences ??= []).Add(difference);

        /// <inheritdoc/>
        public override string ToString()
        {
            if (AreEqual)
                return "No differences detected.";

            var sb = new StringBuilder();
            foreach (var d in _differences!)
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(d.ToString());
            }

            if (IsMaxDifferencesFound)
            {
                sb.AppendLine();
                sb.Append($"Maximum difference count of '{MaxDifferences}' found; comparison stopped.");
            }

            return sb.ToString();
        }
    }
}