// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace System
{
    /// <summary>
    /// Adds <see cref="ToGuid(int)"/> and <see cref="ToLongString(char, int)"/> test-oriented extension methods.
    /// </summary>
    public static class SystemExtensionMethods
    {
        /// <summary>
        /// Converts an <see cref="int"/> to a <see cref="Guid"/>; e.g. '<c>1</c>' will be '<c>00000001-0000-0000-0000-000000000000</c>'.
        /// </summary>
        /// <param name="value">The <see cref="int"/> value.</param>
        /// <returns>The corresponding <see cref="Guid"/>.</returns>
        /// <remarks>Sets the first argument with the <paramref name="value"/> and the remainder with zeroes.</remarks>
        public static Guid ToGuid(this int value) => new(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Creates a long string by repeating the character for the specified count (defaults to 250).
        /// </summary>
        /// <param name="value">The character value.</param>
        /// <param name="count">The repeating count. Defaults to 250.</param>
        /// <returns>The resulting string.</returns>
        public static string ToLongString(this char value, int count = 250) => new(value, count);
    }
}