﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Validation;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public static class ValidationTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidationTester"/> class.
        /// </summary>
        /// <returns>The <see cref="ValidationTester"/>.</returns>
        public static Internal.ValidationTester Create() => new();
    }
}