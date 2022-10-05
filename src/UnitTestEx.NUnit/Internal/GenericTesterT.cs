﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

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
        internal GenericTester() : base(new NUnitTestImplementor()) { }
    }
}