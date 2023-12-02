// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <typeparamref name="TValue"/> <b>Xunit</b> generic testing capability.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint, TValue}"/> class.
    /// </remarks>
    /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
    public class GenericTester<TEntryPoint, TValue>(ITestOutputHelper output) : GenericTesterBase<TEntryPoint, TValue, GenericTester<TEntryPoint, TValue>>(new XunitTestImplementor(output)) where TEntryPoint : class { }
}