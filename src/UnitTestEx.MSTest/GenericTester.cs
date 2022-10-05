﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <returns>The <see cref="GenericTester"/>.</returns>
        public static Internal.GenericTester Create() => new();
    }
}