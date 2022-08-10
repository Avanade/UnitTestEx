// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Validation;
using UnitTestEx.Generic;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public class ValidationTester : ValidationTesterBase<ValidationTester>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTester"/> class.
        /// </summary>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        internal ValidationTester(string? username) : base(new MSTestImplementor(), username) { }
    }
}