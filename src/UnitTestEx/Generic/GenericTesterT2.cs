// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Abstractions;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides the <b>NUnit</b> <typeparamref name="TValue"/> generic testing capability.
    /// </summary>
    public class GenericTester<TEntryPoint, TValue>() : GenericTesterBase<TEntryPoint, TValue, GenericTester<TEntryPoint, TValue>>(TestFrameworkImplementor.Create()) where TEntryPoint : class { }
}