// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the <b>NUnit</b> Function testing capability.
    /// </summary>
    public static class FunctionTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FunctionTester{TEntryPoint}"/> class.
        /// </summary>
        /// <typeparam name="TEntryPoint">The Function startup <see cref="Type"/>.</typeparam>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <returns>The <see cref="FunctionTester{TEntryPoint}"/>.</returns>
        public static FunctionTester<TEntryPoint> Create<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration = null)
            where TEntryPoint : class, new() 
            => new(includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration);
    }
}