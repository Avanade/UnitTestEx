// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Generic;

namespace UnitTestEx.NUnit.Internal
{
    /// <summary>
    /// Provides the <b>NUnit</b> <typeparamref name="TValue"/> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint, TValue>() : GenericTesterBase<TEntryPoint, TValue, GenericTester<TEntryPoint, TValue>>(new Internal.NUnitTestImplementor()) where TEntryPoint : class { }
}