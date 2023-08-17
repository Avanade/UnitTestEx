// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using CoreEx.Validation;
using UnitTestEx.Generic;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public class ValidationTester<TEntryPoint> : ValidationTesterBase<TEntryPoint, ValidationTester<TEntryPoint>> where TEntryPoint : IHostStartup, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTester{TEntryPoint}"/> class.
        /// </summary>
        internal ValidationTester() : base(new MSTestImplementor()) { }
    }
}