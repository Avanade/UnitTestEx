// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides the <typeparamref name="TValue"/> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint, TValue> : GenericTesterBase<TEntryPoint, TValue, GenericTester<TEntryPoint, TValue>> where TEntryPoint : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint, TValue}"/> class using an optional implementor factory.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public GenericTester(Func<TestFrameworkImplementor>? createFactory = null) : this(output: null, createFactory: createFactory) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint, TValue}"/> class using optional test output and an implementor factory.
        /// </summary>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public GenericTester(object? output, Func<TestFrameworkImplementor>? createFactory = null) : base(TestFrameworkImplementor.Create(createFactory, output)) { }
    }
}