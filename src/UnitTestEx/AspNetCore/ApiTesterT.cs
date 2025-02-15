// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Provides the concrete <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
    public class ApiTester<TEntryPoint>(Func<TestFrameworkImplementor>? createFactory = null) : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>>(TestFrameworkImplementor.Create(createFactory)) where TEntryPoint : class { }
}
