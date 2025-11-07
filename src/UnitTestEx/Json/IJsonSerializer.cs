// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Provides the core (common) JSON Serialize and Deserialize capabilities.
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>
        /// Gets the underlying serializer configuration settings/options.
        /// </summary>
        object Options { get; }

        /// <summary>
        /// Serialize the <paramref name="value"/> to a JSON <see cref="string"/>.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="format">Where specified overrides the serialization write formatting.</param>
        /// <returns>The JSON <see cref="string"/>.</returns>
        string Serialize<T>(T value, JsonWriteFormat? format = null);

        /// <summary>
        /// Deserialize the JSON <see cref="string"/> to an underlying JSON object.
        /// </summary>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <returns>The JSON object (as per the underlying implementation).</returns>
        object? Deserialize(string json);

        /// <summary>
        /// Deserialize the JSON <see cref="string"/> to the specified <paramref name="type"/>.
        /// </summary>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <param name="type">The <see cref="Type"/> to convert to.</param>
        /// <returns>The corresponding typed value.</returns>
        object? Deserialize(string json, Type type);

        /// <summary>
        /// Deserialize the JSON <see cref="string"/> to the <see cref="Type"/> of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to convert to.</typeparam>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <returns>The corresponding typed value.</returns>
        T? Deserialize<T>(string json);

        /// <summary>
        /// Clones the <see cref="IJsonSerializer"/>.
        /// </summary>
        /// <returns>The cloned <see cref="IJsonSerializer"/>.</returns>
        IJsonSerializer Clone();
    }
}