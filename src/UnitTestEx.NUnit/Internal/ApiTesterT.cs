﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.AspNetCore;
using NFI = NUnit.Framework.Internal;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    public class ApiTester<TEntryPoint> : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>> where TEntryPoint : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        internal ApiTester(string? username) : base(new Internal.NUnitTestImplementor(NFI.TestExecutionContext.CurrentContext), username) { }
    }
}