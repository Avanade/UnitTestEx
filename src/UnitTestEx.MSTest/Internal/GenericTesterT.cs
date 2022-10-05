﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> generic testing capability.
    /// </summary>
    public class GenericTester : GenericTesterBase<ValidationTester>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        internal GenericTester() : base(new MSTestImplementor()) { }
    }
}