// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Provides the concrete <see cref="AspireTesterBase{TAppHost, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
    public class AspireTester<TAppHost> : AspireTesterBase<TAppHost, AspireTester<TAppHost>> where TAppHost : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspireTester{TAppHost}"/> class using an optional implementor factory.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public AspireTester(Func<TestFrameworkImplementor>? createFactory = null) : this(output: null, createFactory: createFactory) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspireTester{TAppHost}"/> class using optional test output and an implementor factory.
        /// </summary>
        /// <param name="output">The optional framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        public AspireTester(object? output, Func<TestFrameworkImplementor>? createFactory = null) : base(TestFrameworkImplementor.Create(createFactory, output)) { }
    }
}
