// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Aspire;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the .NET Aspire multi-host (distributed application) testing capability.
    /// </summary>
    public static class AspireTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AspireTester{TAppHost}"/> class.
        /// </summary>
        /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="AspireTester{TAppHost}"/>.</returns>
        public static AspireTester<TAppHost> Create<TAppHost>(Func<TestFrameworkImplementor>? createFactory = null) where TAppHost : class => new(createFactory);
    }
}
