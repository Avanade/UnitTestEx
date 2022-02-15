// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using KellermanSoftware.CompareNetObjects;
using System;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Represents the object comparer using <see cref="CompareLogic"/> to perform.
    /// </summary>
    public static class ObjectComparer
    {
        private static Func<ComparisonConfig> _config = () => new ComparisonConfig
        {
            CompareStaticFields = false,
            CompareStaticProperties = false,
            CompareReadOnly = false,
            CompareFields = false,
            MaxDifferences = 20,
            MaxMillisecondsDateDifference = 100,
            IgnoreObjectTypes = true
        };

        /// <summary>
        /// Gets or sets the default <see cref="ComparisonConfig"/> creation function.
        /// </summary>
        public static Func<ComparisonConfig> CreateDefaultConfig
        {
            get => _config;
            set => _config = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ComparisonResult"/>.</returns>
        public static ComparisonResult Compare(object? expected, object? actual, params string[] membersToIgnore)
        {
            var cl = new CompareLogic(CreateDefaultConfig());
            cl.Config.MembersToIgnore.AddRange(membersToIgnore);
            return cl.Compare(expected, actual);
        }

        /// <summary>
        /// Compares two objects of the same <see cref="Type"/> to each other.
        /// </summary>
        /// <param name="comparisonConfig">The action to enable additional <see cref="ComparisonConfig"/> configuration.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ComparisonResult"/>.</returns>
        public static ComparisonResult Compare(Action<ComparisonConfig> comparisonConfig, object? expected, object? actual, params string[] membersToIgnore)
        {
            var cl = new CompareLogic(CreateDefaultConfig());
            comparisonConfig?.Invoke(cl.Config);
            cl.Config.MembersToIgnore.AddRange(membersToIgnore);
            return cl.Compare(expected, actual);
        }
    }
}