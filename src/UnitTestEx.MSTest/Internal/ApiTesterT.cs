// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.AspNetCore;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="ApiTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="System.Type"/>.</typeparam>
    public class ApiTester<TEntryPoint>() : ApiTesterBase<TEntryPoint, ApiTester<TEntryPoint>>(new Internal.MSTestImplementor()) where TEntryPoint : class { }
}