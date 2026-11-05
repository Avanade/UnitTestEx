// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the Function testing capability.
    /// </summary>
    public static class FunctionTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FunctionTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">Any <see cref="Type"/> within the Azure Functions isolated-worker application assembly (e.g. a function class); the application's own entry point (<c>Program.cs</c>) is executed as-is to build the host.</typeparam>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        /// <returns>The <see cref="FunctionTester{TEntryPoint}"/>.</returns>
        public static FunctionTester<TEntryPoint> Create<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration = null, Func<TestFrameworkImplementor>? createFactory = null)
            where TEntryPoint : class
            => new(includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration, createFactory);
    }
}