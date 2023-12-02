// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Collections.Generic;
using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Provides JSON extension methods.
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// Trys to get the <see cref="JsonElement"/> with the <paramref name="propertyName"/> and <paramref name="comparer"/>.
        /// </summary>
        /// <param name="json">The <see cref="JsonElement"/>.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/>.</param>
        /// <param name="value">The named <see cref="JsonElement"/> where found.</param>
        /// <returns><c>true</c> indicates the property was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetProperty(this JsonElement json, string propertyName, IEqualityComparer<string> comparer, out JsonElement value)
        {
            foreach (var j in json.EnumerateObject())
            {
                if (comparer.Equals(j.Name, propertyName))
                {
                    value = j.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
