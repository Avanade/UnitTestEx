// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;
using NFI = NUnit.Framework.Internal;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> generic testing capability.
    /// </summary>
    public class GenericTester : GenericTesterBase<GenericTester>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        internal GenericTester(string? username) : base(new NUnitTestImplementor(NFI.TestExecutionContext.CurrentContext), username) { }
    }
}