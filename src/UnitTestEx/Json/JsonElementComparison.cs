// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Defines the <see cref="JsonElement"/> comparison option where <see cref="JsonElement.ValueKind"/> is either a <see cref="JsonValueKind.String"/> or <see cref="JsonValueKind.Number"/>.
    /// </summary>
    public enum JsonElementComparison
    {
        /// <summary>
        /// Indicates that a semantic match is to used for the comparison.
        /// </summary>
        Semantic,

        /// <summary>
        /// Indicates that an exact match is to used for the comparison.
        /// </summary>
        /// <remarks>Uses the <see cref="JsonElement.GetRawText"/> for the value comparison.</remarks>
        Exact
    }
}