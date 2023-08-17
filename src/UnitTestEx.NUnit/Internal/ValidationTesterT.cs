// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using CoreEx.Validation;
using UnitTestEx.Generic;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public class ValidationTester<TEntryPoint> : ValidationTesterBase<TEntryPoint, ValidationTester<TEntryPoint>> where TEntryPoint : IHostStartup, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTester{TEntryPoint}"/> class.
        /// </summary>
        internal ValidationTester() : base(new NUnitTestImplementor()) { }
    }
}