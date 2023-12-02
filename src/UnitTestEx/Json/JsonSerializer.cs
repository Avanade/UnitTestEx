// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using Stj = System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Provides the <see cref="Stj.JsonSerializer"/> encapsulated implementation.
    /// </summary>
    /// <param name="options">The <see cref="Stj.JsonSerializerOptions"/>. Defaults to <see cref="DefaultOptions"/>.</param>
    public class JsonSerializer(Stj.JsonSerializerOptions? options = null) : IJsonSerializer
    {
        /// <summary>
        /// Gets or sets the default <see cref="Stj.JsonSerializerOptions"/>.
        /// </summary>
        public static Stj.JsonSerializerOptions DefaultOptions { get; set; } = new Stj.JsonSerializerOptions(Stj.JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = Stj.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = false
        };

        /// <summary>
        /// Gets or sets the default <see cref="IJsonSerializer"/>.
        /// </summary>
        public static IJsonSerializer Default { get; set; } = new JsonSerializer();

        /// <inheritdoc/>
        object IJsonSerializer.Options => Options;

        /// <summary>
        /// Gets the <see cref="Stj.JsonSerializerOptions"/>.
        /// </summary>
        public Stj.JsonSerializerOptions Options { get; } = options ?? DefaultOptions;

        /// <inheritdoc/>
        public object? Deserialize(string json) => Stj.JsonSerializer.Deserialize<dynamic>(json, Options);

        /// <inheritdoc/>
        public object? Deserialize(string json, Type type) => Stj.JsonSerializer.Deserialize(json, type, Options);

        /// <inheritdoc/>
        public T? Deserialize<T>(string json) => Stj.JsonSerializer.Deserialize<T>(json, Options)!;

        /// <inheritdoc/>
        public string Serialize<T>(T value, JsonWriteFormat? format = null) => Stj.JsonSerializer.Serialize(value, format == null ? Options : new Stj.JsonSerializerOptions(Options) { WriteIndented = format.Value == JsonWriteFormat.Indented });
    }
}