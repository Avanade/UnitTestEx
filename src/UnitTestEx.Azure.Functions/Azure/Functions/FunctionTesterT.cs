// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides the concrete <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">Any <see cref="System.Type"/> within the Azure Functions isolated-worker application assembly.</typeparam>
    /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
    /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
    /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
    /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
    public class FunctionTester<TEntryPoint>(bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration, Func<TestFrameworkImplementor>? createFactory = null)
        : FunctionTesterBase<TEntryPoint, FunctionTester<TEntryPoint>>(TestFrameworkImplementor.Create(createFactory), includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration) where TEntryPoint : class
    { }
}