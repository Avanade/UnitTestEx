// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the test assert helper where there is no return value; i.e. <see cref="void"/>.
    /// </summary>
    public class VoidAssertor : AssertorBase<VoidAssertor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VoidAssertor"/> class.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> (if any).</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal VoidAssertor(Exception? exception, TestFrameworkImplementor implementor) : base(exception, implementor) { }
    }
}