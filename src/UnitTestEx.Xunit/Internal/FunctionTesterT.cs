// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System.Collections.Generic;
using UnitTestEx.Functions;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Internal
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="TEntryPoint"></typeparam>
    public class FunctionTester<TEntryPoint> : FunctionTesterBase<TEntryPoint, FunctionTester<TEntryPoint>> where TEntryPoint : FunctionsStartup, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        internal FunctionTester(ITestOutputHelper output, bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string>>? additionalConfiguration, string? username) 
            : base(new XunitTestImplementor(output), includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration, username) { }
    }
}
