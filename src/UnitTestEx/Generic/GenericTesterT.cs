// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Abstractions;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides the <b>NUnit</b> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint>() : GenericTesterBase<TEntryPoint, GenericTester<TEntryPoint>>(TestFrameworkImplementor.Create()) where TEntryPoint : class { }
}