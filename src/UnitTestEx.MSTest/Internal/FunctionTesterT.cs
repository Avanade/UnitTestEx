// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System.Collections.Generic;
using UnitTestEx.Functions;

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint">The Function startup <see cref="System.Type"/>.</typeparam>
    /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
    /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
    /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
    public class FunctionTester<TEntryPoint>(bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration)
        : FunctionTesterBase<TEntryPoint, FunctionTester<TEntryPoint>>(new MSTestImplementor(), includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration) where TEntryPoint : FunctionsStartup, new() { }
}