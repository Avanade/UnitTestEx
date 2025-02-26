// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx
{
    /// <summary>
    /// Deep object comparer using <see cref="JsonElementComparer"/>.
    /// </summary>
    /// <remarks>The <see cref="TestSetUp.Default"/> <see cref="TestSetUp.JsonComparerOptions"/> and <see cref="TestSetUp.JsonSerializer"/> are used where not explicitly provided.</remarks>
    public static class ObjectComparer
    {
        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        public static void Assert(object? expected, object? actual, params string[] pathsToIgnore) => Assert(null, expected, actual, pathsToIgnore);

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="options">The <see cref="JsonElementComparerOptions"/>.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        public static void Assert(JsonElementComparerOptions? options, object? expected, object? actual, params string[] pathsToIgnore)
        {
            if (expected is null && actual is null)
                return;

            if (expected is null)
            {
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal: NULL != value.");
                return;
            }

            if (expected is null)
            {
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal: value != NULL.");
                return;
            }

            var o = (options ?? TestSetUp.Default.JsonComparerOptions).Clone();
            o.JsonSerializer ??= TestSetUp.Default.JsonSerializer;

            var cr = new JsonElementComparer(o).CompareValue(expected, actual, pathsToIgnore);
            if (cr.HasDifferences)
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
        }

        /// <summary>
        /// Compares two JSON strings to each other.
        /// </summary>
        /// <param name="expected">The expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        [Obsolete("The JsonAssert is being renamed to AssertJson as this name is standard with UnitTestEx.")]
#if NET7_0_OR_GREATER
        public static void JsonAssert([StringSyntax(StringSyntaxAttribute.Json)] string? expected, [StringSyntax(StringSyntaxAttribute.Json)] string? actual, params string[] pathsToIgnore) => AssertJson(null, expected, actual, pathsToIgnore);
#else
        public static void JsonAssert(string? expected, string? actual, params string[] pathsToIgnore) => AssertJson(null, expected, actual, pathsToIgnore);
#endif

        /// <summary>
        /// Compares two JSON strings to each other.
        /// </summary>
        /// <param name="options">The <see cref="JsonElementComparerOptions"/>.</param>
        /// <param name="expected">The expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        [Obsolete("The JsonAssert is being renamed to AssertJson as this name is standard with UnitTestEx.")]
#if NET7_0_OR_GREATER
        public static void JsonAssert(JsonElementComparerOptions? options, [StringSyntax(StringSyntaxAttribute.Json)] string? expected, [StringSyntax(StringSyntaxAttribute.Json)] string? actual, params string[] pathsToIgnore)
#else
        public static void JsonAssert(JsonElementComparerOptions? options, string? expected, string? actual, params string[] pathsToIgnore)
#endif
            => AssertJson(options, expected, actual, pathsToIgnore);

        /// <summary>
        /// Compares two JSON strings to each other.
        /// </summary>
        /// <param name="expected">The expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
#if NET7_0_OR_GREATER
        public static void AssertJson([StringSyntax(StringSyntaxAttribute.Json)] string? expected, [StringSyntax(StringSyntaxAttribute.Json)] string? actual, params string[] pathsToIgnore) => AssertJson(null, expected, actual, pathsToIgnore);
#else
        public static void AssertJson(string? expected, string? actual, params string[] pathsToIgnore) => AssertJson(null, expected, actual, pathsToIgnore);
#endif

        /// <summary>
        /// Compares two JSON strings to each other.
        /// </summary>
        /// <param name="options">The <see cref="JsonElementComparerOptions"/>.</param>
        /// <param name="expected">The expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
#if NET7_0_OR_GREATER
        public static void AssertJson(JsonElementComparerOptions? options, [StringSyntax(StringSyntaxAttribute.Json)] string? expected, [StringSyntax(StringSyntaxAttribute.Json)] string? actual, params string[] pathsToIgnore)
#else
        public static void AssertJson(JsonElementComparerOptions? options, string? expected, string? actual, params string[] pathsToIgnore)
#endif
        {
            if (expected is null && actual is null)
                return;

            if (expected is null)
            {
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal: NULL != value.");
                return;
            }

            if (actual is null)
            {
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal: value != NULL.");
                return;
            }

            var o = (options ?? TestSetUp.Default.JsonComparerOptions).Clone();
            o.JsonSerializer ??= TestSetUp.Default.JsonSerializer;

            var cr = new JsonElementComparer(o).Compare(expected, actual, pathsToIgnore);
            if (cr.HasDifferences)
                TestFrameworkImplementor.Create().AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
        }

        /// <summary>
        /// Compares two JSON strings to each other where the expected JSON is from an embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        public static void AssertJsonFromResource(string resourceName, string? actual, params string[] pathsToIgnore)
            => AssertJson(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), actual, pathsToIgnore);

        /// <summary>
        /// Compares two JSON strings to each other where the expected JSON is from an embedded resource.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected JSON.</param>
        /// <param name="actual">The actual JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        public static void AssertJsonFromResource<TAssembly>(string resourceName, string? actual, params string[] pathsToIgnore)
            => AssertJson(Resource.GetJson(resourceName, typeof(TAssembly).Assembly), actual, pathsToIgnore);
    }
}