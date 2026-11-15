// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the concrete <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    public class ApiTester<TEntryPoint> : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>> where TEntryPoint : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTester{TEntryPoint}"/> class using an optional implementor factory.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public ApiTester(Func<TestFrameworkImplementor>? createFactory = null) : this(output: null, createFactory: createFactory) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTester{TEntryPoint}"/> class using optional test output and an implementor factory.
        /// </summary>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public ApiTester(object? output, Func<TestFrameworkImplementor>? createFactory = null) : base(TestFrameworkImplementor.Create(createFactory, output)) { }
    }
}
