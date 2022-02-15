// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using UnitTestEx.NUnit.Internal;

namespace UnitTestEx.NUnit
{
    /// <summary>
    /// Provides the <b>NUnit</b> Function testing capability.
    /// </summary>
    public static class FunctionTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <returns>The <see cref="ApiTester{TEntryPoint}"/>.</returns>
        public static FunctionTester<TEntryPoint> Create<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, params KeyValuePair<string, string>[] additionalConfiguration)
            where TEntryPoint : FunctionsStartup, new() 
            => new(includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration);
    }
}