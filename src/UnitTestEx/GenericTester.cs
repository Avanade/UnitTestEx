// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Generic;
using UnitTestEx.Hosting;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the <b>NUnit</b> generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<object> Create() => Create<object>();

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<TEntryPoint> Create<TEntryPoint>() where TEntryPoint : class => new();

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<object, TValue> CreateFor<TValue>() => CreateFor<object, TValue>();

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<TEntryPoint, TValue> CreateFor<TEntryPoint, TValue>() where TEntryPoint : class => new();
    }
}