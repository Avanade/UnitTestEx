// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Generic;
using UnitTestEx.Hosting;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<object> Create(Func<TestFrameworkImplementor>? createFactory = null) => Create<object>(createFactory);

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class using the specified test output.
        /// </summary>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<object> Create(object? output, Func<TestFrameworkImplementor>? createFactory = null) => Create<object>(output, createFactory);

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<TEntryPoint> Create<TEntryPoint>(Func<TestFrameworkImplementor>? createFactory = null) where TEntryPoint : class => new(createFactory);

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester{TEntryPoint}"/> class using the specified test output.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public static GenericTester<TEntryPoint> Create<TEntryPoint>(object? output, Func<TestFrameworkImplementor>? createFactory = null) where TEntryPoint : class => new(output, createFactory);

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<object, TValue> CreateFor<TValue>(Func<TestFrameworkImplementor>? createFactory = null) => CreateFor<object, TValue>(createFactory);

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class using the specified test output.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<object, TValue> CreateFor<TValue>(object? output, Func<TestFrameworkImplementor>? createFactory = null) => CreateFor<object, TValue>(output, createFactory);

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<TEntryPoint, TValue> CreateFor<TEntryPoint, TValue>(Func<TestFrameworkImplementor>? createFactory = null) where TEntryPoint : class => new(createFactory);

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TValue"/> <see cref="GenericTester{TEntryPoint, TValue}"/> class using the specified test output.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="GenericTester{TEntryPoint, TValue}"/>.</returns>
        public static GenericTester<TEntryPoint, TValue> CreateFor<TEntryPoint, TValue>(object? output, Func<TestFrameworkImplementor>? createFactory = null) where TEntryPoint : class => new(output, createFactory);
    }
}