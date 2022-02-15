// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using KellermanSoftware.CompareNetObjects;
using System;
using UnitTestEx.NUnit.Internal;
using NFI = NUnit.Framework.Internal;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Deep object comparer.
    /// </summary>
    public static class ObjectComparer
    {
        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public static void Assert(object? expected, object? actual, params string[] membersToIgnore)
        {
            var cr = Abstractions.ObjectComparer.Compare(expected, actual, membersToIgnore);
            new NUnitTestImplementor(NFI.TestExecutionContext.CurrentContext).AssertAreEqual(true, cr.AreEqual, cr.DifferencesString);
        }

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="comparisonConfig">The action to enable additional <see cref="ComparisonConfig"/> configuration.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public static void Assert(Action<ComparisonConfig> comparisonConfig, object? expected, object? actual, params string[] membersToIgnore)
        {
            var cr = Abstractions.ObjectComparer.Compare(comparisonConfig, expected, actual, membersToIgnore);
            new NUnitTestImplementor(NFI.TestExecutionContext.CurrentContext).AssertAreEqual(true, cr.AreEqual, cr.DifferencesString);
        }
    }
}