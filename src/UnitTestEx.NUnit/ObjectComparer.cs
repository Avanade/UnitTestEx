// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Json;
using UnitTestEx.NUnit.Internal;

namespace UnitTestEx.NUnit
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
                new NUnitTestImplementor().AssertFail($"Expected and Actual values are not equal: NULL != value.");
                return;
            }

            if (expected is null)
            {
                new NUnitTestImplementor().AssertFail($"Expected and Actual values are not equal: value != NULL.");
                return;
            }

            var o = (options ?? TestSetUp.Default.JsonComparerOptions).Clone();
            o.JsonSerializer ??= TestSetUp.Default.JsonSerializer;

            var cr = new JsonElementComparer(o).CompareValue(expected, actual, pathsToIgnore);
            if (cr.HasDifferences)
                new NUnitTestImplementor().AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
        }
    }
}