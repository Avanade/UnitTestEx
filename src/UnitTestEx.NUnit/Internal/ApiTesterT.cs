// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    public class ApiTester<TEntryPoint>() : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>>(new Internal.NUnitTestImplementor()) where TEntryPoint : class { }
}