// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace UnitTestEx.Json
{
    /// <summary>
    /// Provides the options for <see cref="JsonElementComparer"/>.
    /// </summary>
    public sealed class JsonElementComparerOptions
    {
        private static JsonElementComparerOptions? _default;

        /// <summary>
        /// Gets or sets the default <see cref="JsonElementComparer"/> instance.
        /// </summary>
        public static JsonElementComparerOptions Default
        {
            get => _default ??= new();
            set => _default = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the optional preamble text that provides context for the comparison in the resulting error message.
        /// </summary>
        public string? PreambleText { get; set; } = null;

        /// <summary>
        /// Gets or sets the <see cref="IEqualityComparer{String}"/> to use for comparing JSON paths.
        /// </summary>
        /// <remarks>Defaults to <see cref="StringComparer.OrdinalIgnoreCase"/>.</remarks>
        public IEqualityComparer<string> PathComparer { get; set; } = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Gets or sets the <see cref="IEqualityComparer{String}"/> to use for comparing property names.
        /// </summary>
        /// <remarks>Where not specified will use the native (fast) <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> exact comparison; otherwise, will use 
        /// <see cref="JsonExtensions.TryGetProperty(JsonElement, string, IEqualityComparer{string}, out JsonElement)"/> which is less performant (however, enables semantic comparison where applicable).</remarks>
        public IEqualityComparer<string>? PropertyNameComparer { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of differences to detect where performing a comparison.
        /// </summary>
        /// <remarks>Defaults to <see cref="int.MaxValue"/>.</remarks>
        public int MaxDifferences { get; set; } = int.MaxValue;

        /// <summary>
        /// Gets or sets the <see cref="JsonElementComparison"/> used for property value comparisons.
        /// </summary>
        /// <remarks>When <see cref="JsonElementComparison.Semantic"/>: a <see cref="JsonValueKind.String"/> the comparison will be performed using <see cref="JsonElement.GetDateTimeOffset"/>, <see cref="JsonElement.GetDateTime"/>, <see cref="JsonElement.GetGuid"/> and <see cref="JsonElement.GetString"/>
        /// (in order specified) until match found; otherwise, for a <see cref="JsonValueKind.Number"/> the comparison will be performed using <see cref="JsonElement.GetDecimal"/> and <see cref="JsonElement.GetDouble"/> (in order specified) until a match
        /// found.<para>Defaults to <see cref="JsonElementComparison.Semantic"/>.</para></remarks>
        public JsonElementComparison ValueComparison { get; set; } = JsonElementComparison.Semantic;

        /// <summary>
        /// Gets or sets the <see cref="JsonElementComparison"/> used for null value comparisons.
        /// </summary>
        /// <remarks>When <see cref="JsonElementComparison.Semantic"/>: a <see cref="JsonValueKind.Null"/> where the other property does not exist assumes is equivalent null by default.</remarks>
        public JsonElementComparison NullComparison { get; set; } = JsonElementComparison.Exact;

        /// <summary>
        /// Gets or sets the <see cref="IJsonSerializer"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="JsonSerializer.Default"/> where not specified.</remarks>
        public IJsonSerializer? JsonSerializer { get; set; }

        /// <summary>
        /// Clones the <see cref="JsonElementComparerOptions"/>.
        /// </summary>
        /// <returns>A new (cloned) instance.</returns>
        public JsonElementComparerOptions Clone() => new()
        {
            PathComparer = PathComparer,
            PropertyNameComparer = PropertyNameComparer,
            MaxDifferences = MaxDifferences,
            ValueComparison = ValueComparison,
            NullComparison = NullComparison,
            JsonSerializer = JsonSerializer?.Clone()
        };
    }
}