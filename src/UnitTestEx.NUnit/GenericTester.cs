// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using System;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Provides the <b>NUnit</b> generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint}"/>.</returns>
        public static Internal.GenericTester<HostStartup> Create() => Create<HostStartup>();

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="Internal.GenericTester{TEntryPoint}"/>.</returns>
        public static Internal.GenericTester<TEntryPoint> Create<TEntryPoint>() where TEntryPoint : IHostStartup, new() => new();
    }
}