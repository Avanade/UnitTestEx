// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.AspNetCore;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>MSUnit</b> <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint"></typeparam>
    /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
    public class ApiTester<TEntryPoint>(ITestOutputHelper output) : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>>(new Internal.XunitTestImplementor(output)) where TEntryPoint : class { }
}
