// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Provides the concrete <see cref="AspireTesterBase{TAppHost, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
    /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
    public class AspireTester<TAppHost>(Func<TestFrameworkImplementor>? createFactory = null) : AspireTesterBase<TAppHost, AspireTester<TAppHost>>(TestFrameworkImplementor.Create(createFactory)) where TAppHost : class { }
}
