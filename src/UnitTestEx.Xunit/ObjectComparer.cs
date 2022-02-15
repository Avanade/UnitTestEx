// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using KellermanSoftware.CompareNetObjects;
using System;
using UnitTestEx.Xunit.Internal;

namespace UnitTestEx.Xunit
{
    /// <summary>
    /// Deep object comparer.
    /// </summary>
    public class ObjectComparer
    {
        private readonly XunitTestImplementor _implementor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectComparer"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="XunitTestImplementor"/>.</param>
        internal ObjectComparer(XunitTestImplementor implementor) => _implementor = implementor;

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public void Assert(object? expected, object? actual, params string[] membersToIgnore)
        {
            var cr = Abstractions.ObjectComparer.Compare(expected, actual, membersToIgnore);
            _implementor.AssertAreEqual(true, cr.AreEqual, cr.DifferencesString);
        }

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="comparisonConfig">The action to enable additional <see cref="ComparisonConfig"/> configuration.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public void Assert(Action<ComparisonConfig> comparisonConfig, object? expected, object? actual, params string[] membersToIgnore)
        {
            var cr = Abstractions.ObjectComparer.Compare(comparisonConfig, expected, actual, membersToIgnore);
            _implementor.AssertAreEqual(true, cr.AreEqual, cr.DifferencesString);
        }
    }
}