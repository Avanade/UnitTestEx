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
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        public static ApiTester<TEntryPoint> Create<TEntryPoint>(string? username = null) where TEntryPoint : class => new(username);
    }
}