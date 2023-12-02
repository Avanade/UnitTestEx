// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Hosting;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Internal.GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint}"/>.</returns>
        public static Internal.GenericTester<object> Create() => Create<object>();

        /// <summary>
        /// Creates a new instance of the <see cref="Internal.GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint}"/>.</returns>
        public static Internal.GenericTester<TEntryPoint> Create<TEntryPoint>() where TEntryPoint : class => new();

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="Internal.GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static Internal.GenericTester<object, TValue> CreateFor<TValue>() => CreateFor<object, TValue>();

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="Internal.GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static Internal.GenericTester<TEntryPoint, TValue> CreateFor<TEntryPoint, TValue>() where TEntryPoint : class => new();
    }
}