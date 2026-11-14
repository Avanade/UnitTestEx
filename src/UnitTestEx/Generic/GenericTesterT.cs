// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides the generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint> : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>> where TEntryPoint : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint}"/> class using an optional implementor factory.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public GenericTester(Func<TestFrameworkImplementor>? createFactory = null) : this(output: null, createFactory: createFactory) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint}"/> class using optional test output and an implementor factory.
        /// </summary>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public GenericTester(object? output, Func<TestFrameworkImplementor>? createFactory = null) : base(TestFrameworkImplementor.Create(createFactory, output)) { }
    }
}