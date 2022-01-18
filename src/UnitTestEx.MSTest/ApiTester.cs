// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.MSTest.Internal;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> API testing capability.
    /// </summary>
    public static class ApiTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ApiTester{TEntryPoint}"/>.</returns>
        public static ApiTester<TEntryPoint> Create<TEntryPoint>() where TEntryPoint : class => new();
    }
}