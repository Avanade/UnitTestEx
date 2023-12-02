// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint>() : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>>(new Internal.NUnitTestImplementor()) where TEntryPoint : class { }
}