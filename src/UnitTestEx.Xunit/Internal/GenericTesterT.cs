// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using UnitTestEx.Generic;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>Xunit</b> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint> : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>> where TEntryPoint : IHostStartup, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        internal GenericTester(ITestOutputHelper output) : base(new XunitTestImplementor(output)) { }
    }
}