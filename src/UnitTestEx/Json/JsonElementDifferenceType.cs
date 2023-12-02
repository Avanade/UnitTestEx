// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Defines the type of difference identified.
    /// </summary>
    public enum JsonElementDifferenceType
    {
        /// <summary>
        /// Indicates that the left and right <see cref="JsonElement"/> <see cref="JsonValueKind"/> is different.
        /// </summary>
        Kind,

        /// <summary>
        /// Indicates that the left and right <see cref="JsonElement"/> values are different.
        /// </summary>
        Value,

        /// <summary>
        /// Indicates that the corresponding path does not exist in the left <see cref="JsonElement"/>.
        /// </summary>
        LeftNone,

        /// <summary>
        /// Indicates that the corresponding path does not exist in the right <see cref="JsonElement"/>.
        /// </summary>
        RightNone,

        /// <summary>
        /// Indicates that the left and right <see cref="JsonElement"/> array lengths are different.
        /// </summary>
        ArrayLength
    }
}