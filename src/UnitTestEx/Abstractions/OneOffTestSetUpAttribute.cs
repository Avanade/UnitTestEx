// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Marks a class to be run once before all tests in the assembly.
    /// </summary>
    /// <param name="type">The <see cref="OneOffTestSetUpBase"/> <see cref="Type"/> to invoke.</param>
    /// <exception cref="ArgumentNullException"></exception>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public class OneOffTestSetUpAttribute(Type type) : Attribute
    {
        /// <summary>
        /// Performs the set up for the specified <paramref name="assembly"/> where configured using the <see cref="OneOffTestSetUpAttribute"/>.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        public static void SetUp(Assembly assembly)
        {
            var att = assembly.GetCustomAttributes(typeof(OneOffTestSetUpAttribute), false);
            if (att.Length > 0 && att[0] is OneOffTestSetUpAttribute sua)
                sua.SetUp();
        }

        /// <summary>
        /// Gets the <see cref="OneOffTestSetUpBase"/> <see cref="Type"/>.
        /// </summary>
        public Type Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

        /// <summary>
        /// Performs the one-off test set up using the specified <see cref="Type"/>.
        /// </summary>
        public void SetUp() => ((Activator.CreateInstance(Type) as OneOffTestSetUpBase) ?? throw new InvalidOperationException($"Type {Type.Name} must inherit from {nameof(OneOffTestSetUpBase)}.")).SetUp();
    }
}