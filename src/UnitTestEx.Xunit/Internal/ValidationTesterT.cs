// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using CoreEx.Validation;
using UnitTestEx.Generic;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>Xunit</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public class ValidationTester<TEntryPoint> : ValidationTesterBase<TEntryPoint, ValidationTester<TEntryPoint>> where TEntryPoint : IHostStartup, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        internal ValidationTester(ITestOutputHelper output) : base(new XunitTestImplementor(output)) { }
    }
}