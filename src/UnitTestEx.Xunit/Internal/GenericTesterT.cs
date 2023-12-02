// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>Xunit</b> generic testing capability.
    /// </summary>
    /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
    public class GenericTester<TEntryPoint>(ITestOutputHelper output) : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>>(new XunitTestImplementor(output)) where TEntryPoint : class { }
}