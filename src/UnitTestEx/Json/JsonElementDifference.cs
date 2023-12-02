// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Represents a <see cref="JsonElementComparerResult"/> <see cref="JsonElement"/> comparison difference.
    /// </summary>
    public readonly struct JsonElementDifference
    {
        /// <summary>
        /// Initializes a the <see cref="JsonElementDifference"/> struct.
        /// </summary>
        /// <param name="path">The JSON path.</param>
        /// <param name="left">The left <see cref="JsonElement"/> where applicable.</param>
        /// <param name="right">The right <see cref="JsonElement"/> where applicable.</param>
        /// <param name="type">The <see cref="JsonElementDifferenceType"/>.</param>
        internal JsonElementDifference(string path, JsonElement? left, JsonElement? right, JsonElementDifferenceType type)
        {
            Path = path;
            Left = left;
            Right = right;
            Type = type;
        }

        /// <summary>
        /// Gets the JSON path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the left <see cref="JsonElement"/> where applicable.
        /// </summary>
        public JsonElement? Left { get; }

        /// <summary>
        /// Gets the right <see cref="JsonElement"/> where applicable.
        /// </summary>
        public JsonElement? Right { get; }

        /// <summary>
        /// Gets the <see cref="JsonElementDifferenceType"/>.
        /// </summary>
        public JsonElementDifferenceType Type { get; }

        /// <inheritdoc/>
        public override string ToString() => $"Path '{Path}': {Type switch
        {
            JsonElementDifferenceType.LeftNone => "Does not exist in left JSON.",
            JsonElementDifferenceType.RightNone => "Does not exist in right JSON.",
            JsonElementDifferenceType.ArrayLength => $"Array lengths are not equal: {Left?.GetArrayLength()} != {Right?.GetArrayLength()}.",
            JsonElementDifferenceType.Kind => $"Kind is not equal: {Left?.ValueKind} != {Right?.ValueKind}.",
            _ => $"Value is not equal: {Left?.GetRawText()} != {Right?.GetRawText()}.",
        }}";
    }
}