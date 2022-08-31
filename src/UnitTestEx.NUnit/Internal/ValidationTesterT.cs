// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Validation;
using UnitTestEx.Generic;
using NFI = NUnit.Framework.Internal;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public class ValidationTester : ValidationTesterBase<ValidationTester>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTester"/> class.
        /// </summary>
        internal ValidationTester() : base(new NUnitTestImplementor(NFI.TestExecutionContext.CurrentContext)) { }
    }
}