// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;
using UnitTestEx.AspNetCore;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the API testing capability.
    /// </summary>
    public static class ApiTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="ApiTester{TEntryPoint}"/>.</returns>
        public static ApiTester<TEntryPoint> Create<TEntryPoint>(Func<TestFrameworkImplementor>? createFactory = null) where TEntryPoint : class => new(createFactory);
    }
}