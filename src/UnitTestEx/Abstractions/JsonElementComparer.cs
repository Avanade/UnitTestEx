// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a <see cref="JsonElement"/> comparer where property order is not significant.
    /// </summary>
    /// <remarks>Influenced by <see href="https://stackoverflow.com/questions/60580743/what-is-equivalent-in-jtoken-deepequals-in-system-text-json"/>.</remarks>
    public sealed class JsonElementComparer : IEqualityComparer<JsonElement>, IEqualityComparer<string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonElementComparer"/> class.
        /// </summary>
        /// <param name="maxDifferences">The maximum number of differences to detect where performing a <see cref="Compare(JsonElement, JsonElement, CompareArgs)"/> or <see cref="Compare(JsonElement, JsonElement, string[])"/>.</param>
        public JsonElementComparer(int maxDifferences = 1) => MaxDifferences = maxDifferences;

        /// <summary>
        /// Gets or sets the maximum number of differences to detect where performing a <see cref="Compare(JsonElement, JsonElement, CompareArgs)"/> or <see cref="Compare(JsonElement, JsonElement, string[])"/>.
        /// </summary>
        /// <remarks>Defaults to '<c>1</c>'.</remarks>
        public int MaxDifferences { get; set; }

        /// <summary>
        /// Compare two JSON strings for equality.
        /// </summary>
        /// <param name="left">The left JSON <see cref="string"/>.</param>
        /// <param name="right">The right JSON <see cref="string"/>.</param>
        /// <param name="pathsToIgnore">Optional list of paths to exclude from the comparison.</param>
        /// <returns>The resulting comparison error message; <c>null</c> indicates equality.</returns>
        /// <exception cref="ArgumentException"></exception>
        public string? Compare(string left, string right, params string[] pathsToIgnore)
        {
            var ljr = new Utf8JsonReader(new BinaryData(left));
            if (!JsonElement.TryParseValue(ref ljr, out JsonElement? lje))
                throw new ArgumentException("JSON is not considered valid.", nameof(left));

            var rjr = new Utf8JsonReader(new BinaryData(right));
            if (!JsonElement.TryParseValue(ref rjr, out JsonElement? rje))
                throw new ArgumentException("JSON is not considered valid.", nameof(right));

            return Compare(lje.Value, rje.Value, pathsToIgnore);
        }

        /// <summary>
        /// Compare two <see cref="JsonElement"/> objects for equality.
        /// </summary>
        /// <param name="left">The left <see cref="JsonElement"/>.</param>
        /// <param name="right">The right <see cref="JsonElement"/>.</param>
        /// <param name="pathsToIgnore">Optional list of paths to exclude from the comparison.</param>
        /// <returns>The resulting comparison error message; <c>null</c> indicates equality.</returns>
        public string? Compare(JsonElement left, JsonElement right, params string[] pathsToIgnore)
        {
            var args = new CompareArgs(MaxDifferences, pathsToIgnore);
            Compare(left, right, args);
            return args.ErrorMessage;
        }

        /// <summary>
        /// Perform the <see cref="JsonElement"/> comparison.
        /// </summary>
        private static void Compare(JsonElement left, JsonElement right, CompareArgs args)
        {
            if (left.ValueKind != right.ValueKind)
            {
                args.AddError(left, right);
                return;
            }

            switch (left.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    // These are the same by kind, so carry on!
                    break;

                case JsonValueKind.String:
                    // Use GetString() vs GetRawText() to resolve JSON escaping.
                    if (left.GetString() != right.GetString())
                        args.AddError(left, right);

                    break;

                case JsonValueKind.Number:
                    // Use GetDecimal() to compare actual number regardless of formatting.
                    if (left.GetDecimal() != right.GetDecimal())
                        args.AddError(left, right);

                    break;

                case JsonValueKind.Object:
                    var lprops = left.EnumerateObject().ToList();
                    var rprops = right.EnumerateObject().ToList();

                    foreach (var l in lprops)
                    {
                        args.Compare(l.Name, () =>
                        {
                            if (right.TryGetProperty(l.Name, out var r))
                                Compare(l.Value, r, args);
                            else
                                args.AddError("does not exist in right JSON value");
                        });

                        if (args.MaxDifferencesFound)
                            break;
                    }

                    foreach (var r in rprops)
                    {
                        args.Compare(r.Name, () =>
                        {
                            if (!left.TryGetProperty(r.Name, out _))
                                args.AddError("does not exist in left JSON value");
                        });

                        if (args.MaxDifferencesFound)
                            break;
                    }

                    break;

                case JsonValueKind.Array:
                    var ll = left.EnumerateArray().ToList();
                    var rl = right.EnumerateArray().ToList();
                    if (ll.Count != rl.Count)
                    {
                        args.AddError($"array lengths not equal: {ll.Count} != {rl.Count}");
                        break;
                    }

                    for (int i = 0; i < ll.Count; i++)
                    {
                        args.Compare(i, () => Compare(ll[i], rl[i], args));
                        if (args.MaxDifferencesFound)
                            break;
                    }

                    break;

                case JsonValueKind.Undefined:
                    // Ignore Undefined, assume irrelevant (i.e. not included in comparison).
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected JsonValueKind {left.ValueKind}.");
            }
        }

        /// <inheritdoc/>
        public bool Equals(string? x, string? y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;

            var ljr = new Utf8JsonReader(new BinaryData(x));
            if (!JsonElement.TryParseValue(ref ljr, out JsonElement? lje))
                throw new ArgumentException("JSON is not considered valid.", nameof(x));

            var rjr = new Utf8JsonReader(new BinaryData(y));
            if (!JsonElement.TryParseValue(ref rjr, out JsonElement? rje))
                throw new ArgumentException("JSON is not considered valid.", nameof(y));

            var args = new CompareArgs(1);
            Compare(lje.Value, rje.Value, args);
            return !args.MaxDifferencesFound;
        }

        /// <inheritdoc/>
        public bool Equals(JsonElement x, JsonElement y)
        {
            var args = new CompareArgs(1);
            Compare(x, y, args);
            return !args.MaxDifferencesFound;
        }

        /// <inheritdoc/>
        public int GetHashCode(string json)
        {
            if (json == null)
                return 0;

            var jr = new Utf8JsonReader(new BinaryData(json));
            if (!JsonElement.TryParseValue(ref jr, out JsonElement? je))
                throw new ArgumentException("JSON is not considered valid.", nameof(json));

            return GetHashCode(je.Value);
        }

        /// <inheritdoc/>
        public int GetHashCode(JsonElement json)
        {
            var hash = new HashCode();
            ComputeHashCode(json, ref hash);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Computes the hash code.
        /// </summary>
        private void ComputeHashCode(JsonElement json, ref HashCode hash)
        {
            hash.Add(json.ValueKind);

            switch (json.ValueKind)
            {
                case JsonValueKind.Null:
                    break;

                case JsonValueKind.True:
                    hash.Add(true.GetHashCode());
                    break;

                case JsonValueKind.False:
                    hash.Add(false.GetHashCode());
                    break;

                case JsonValueKind.Number:
                    hash.Add(json.GetDecimal().GetHashCode());
                    break;

                case JsonValueKind.String:
                    hash.Add(json.GetString());
                    break;

                case JsonValueKind.Array:
                    foreach (var item in json.EnumerateArray())
                    {
                        ComputeHashCode(item, ref hash);
                    }

                    break;

                case JsonValueKind.Object:
                    foreach (var property in json.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        hash.Add(property.Name);
                        ComputeHashCode(property.Value, ref hash);
                    }

                    break;

                case JsonValueKind.Undefined:
                    break;

                default:
                    throw new JsonException(string.Format("Unknown JsonValueKind {0}", json.ValueKind));
            }
        }

        /// <summary>
        /// Provides arguments needed to support the comparison.
        /// </summary>
        private class CompareArgs
        {
            private readonly Stack<string> _path = new();
            private readonly Stack<string> _qualifiedPath = new();
            private StringBuilder? _errorMessage;

            /// <summary>
            /// Initializes a new instance of the <see cref="CompareArgs"/> class.
            /// </summary>
            /// <param name="maxDifferences">The maximum number of differences to detect.</param>
            /// <param name="pathsToIgnore">The paths to ignore from the comparison.</param>
            public CompareArgs(int maxDifferences, params string[] pathsToIgnore)
            {
                MaxDifferences = maxDifferences;
                var maxDepth = 0;
                PathsToIgnore = new(CoreEx.Text.Json.JsonFilterer.CreateDictionary(pathsToIgnore, CoreEx.Json.JsonPropertyFilter.Exclude, StringComparison.OrdinalIgnoreCase, ref maxDepth).Keys);
            }

            /// <summary>
            /// Indicates whether to fail fast after first error; versus, report all.
            /// </summary>
            public int MaxDifferences { get; }

            /// <summary>
            /// Gets the current difference count.
            /// </summary>
            public int DifferenceCount { get; private set; }

            /// <summary>
            /// Indicates whether the <see cref="DifferenceCount"/> equals the <see cref="MaxDifferences"/>.
            /// </summary>
            public bool MaxDifferencesFound => DifferenceCount >= MaxDifferences;

            /// <summary>
            /// Get paths to exclude.
            /// </summary>
            public HashSet<string> PathsToIgnore { get; }

            /// <summary>
            /// Gets or sets the current path.
            /// </summary>
            public string? Path => _path.Count == 0 ? null : _path.Peek();

            /// <summary>
            /// Gets or sets the qualified path (includes indexing).
            /// </summary>
            public string? QualifiedPath => _qualifiedPath.Count == 0 ? null : _qualifiedPath.Peek();

            /// <summary>
            /// Gets the error message.
            /// </summary>
            public string? ErrorMessage => _errorMessage?.ToString();

            /// <summary>
            /// Encapsulates a property comparison.
            /// </summary>
            /// <param name="name">The property name.</param>
            /// <param name="action">The action to execute.</param>
            public void Compare(string name, Action action)
            {
                var path = Path == null ? name : $"{Path}.{name}";
                if (PathsToIgnore.Contains(path))
                    return;

                _path.Push(path);
                _qualifiedPath.Push(QualifiedPath == null ? name : $"{QualifiedPath}.{name}");

                action.Invoke();

                _path.Pop();
                _qualifiedPath.Pop();
            }

            /// <summary>
            /// Encapsulates an array item comparison.
            /// </summary>
            /// <param name="index">The array index.</param>
            /// <param name="action">The action to execute.</param>
            public void Compare(int index, Action action)
            {
                _qualifiedPath.Push($"{QualifiedPath}[{index}]");

                action.Invoke();

                _qualifiedPath.Pop();
            }

            /// <summary>
            /// Adds the standard not equal error.
            /// </summary>
            /// <param name="left">The left <see cref="JsonElement"/>.</param>
            /// <param name="right">The right <see cref="JsonElement"/>.</param>
            public void AddError(JsonElement left, JsonElement right) => AddError($"value is not equal: {left.GetRawText()} != {right.GetRawText()}");

            /// <summary>
            /// Adds the specified error <paramref name="message"/>.
            /// </summary>
            /// <param name="message">The error message.</param>
            public void AddError(string message)
            {
                DifferenceCount++;
                if (_errorMessage == null)
                    _errorMessage = new();
                else
                    _errorMessage.AppendLine();

                _errorMessage.Append($"Path '{QualifiedPath}' {message}");

                if (MaxDifferencesFound)
                {
                    _errorMessage.AppendLine();
                    _errorMessage.Append($"Maximum difference count of '{MaxDifferences}' found; comparison stopped.");
                }
            }
        }
    }
}