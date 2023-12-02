// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint>() : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>>(new MSTestImplementor()) where TEntryPoint : class { }
}