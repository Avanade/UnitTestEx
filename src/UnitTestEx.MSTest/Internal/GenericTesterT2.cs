// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <typeparamref name="TValue"/> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint, TValue>() : GenericTesterBase<TEntryPoint, TValue, GenericTester<TEntryPoint, TValue>>(new MSTestImplementor()) where TEntryPoint : class { }
}