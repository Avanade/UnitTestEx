// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using UnitTestEx.Mocking;
using UnitTestEx.Xunit.Internal;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit
{
    /// <summary>
    /// Provides the base test capabilities.
    /// </summary>
    public abstract class UnitTestBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestBase"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        protected UnitTestBase(ITestOutputHelper output) => Output = output ?? throw new ArgumentNullException(nameof(output));

        /// <summary>
        /// Gets the <see cref="ITestOutputHelper"/>.
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// Provides the <b>Xunit</b> <see cref="MockHttpClientFactory"/> capability.
        /// </summary>
        /// <returns>The <see cref="MockHttpClientFactory"/>.</returns>
        protected MockHttpClientFactory CreateMockHttpClientFactory() => new(new XunitTestImplementor(Output));

        /// <summary>
        /// Provides the <b>Xunit</b> API testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ApiTester{TEntryPoint}"/>.</returns>
        protected ApiTester<TEntryPoint> CreateApiTester<TEntryPoint>() where TEntryPoint : class => new(Output);

        /// <summary>
        /// Provides the <b>Xunit</b> API testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="FunctionTester{TEntryPoint}"/>.</returns>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        protected FunctionTester<TEntryPoint> CreateFunctionTester<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, params KeyValuePair<string, string>[] additionalConfiguration)
            where TEntryPoint : FunctionsStartup, new()
            => new(Output, includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration);

        /// <summary>
        /// Gets the <see cref="UnitTestEx.Xunit.ObjectComparer"/>.
        /// </summary>
        protected UnitTestEx.Xunit.ObjectComparer ObjectComparer => new UnitTestEx.Xunit.ObjectComparer(new XunitTestImplementor(Output));
    }
}