// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using CoreEx.Validation;
using System;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public static class ValidationTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidationTester"/> class.
        /// </summary>
        /// <returns>The <see cref="Internal.ValidationTester{TEntryPoint}"/>.</returns>
        /// <returns>The <see cref="ValidationTester"/>.</returns>
        public static Internal.ValidationTester<HostStartup> Create() => Create<HostStartup>();

        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="Internal.ValidationTester{TEntryPoint}"/>.</returns>
        public static Internal.ValidationTester<TEntryPoint> Create<TEntryPoint>() where TEntryPoint : IHostStartup, new() => new();
    }
}