// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using UnitTestEx.Hosting;
using UnitTestEx.Mocking;
using UnitTestEx.Xunit.Internal;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit
{
    /// <summary>
    /// Provides the base test capabilities.
    /// </summary>
    /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
    public abstract class UnitTestBase(ITestOutputHelper output)
    {
        /// <summary>
        /// Gets the <see cref="ITestOutputHelper"/>.
        /// </summary>
        protected ITestOutputHelper Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

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
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <returns>The <see cref="FunctionTester{TEntryPoint}"/>.</returns>
        protected FunctionTester<TEntryPoint> CreateFunctionTester<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration = null)
            where TEntryPoint : FunctionsStartup, new()
            => new(Output, includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration);

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public GenericTester<object> CreateGenericTester() => CreateGenericTester<object>();

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        protected GenericTester<TEntryPoint> CreateGenericTester<TEntryPoint>() where TEntryPoint : class => new(Output);

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        public GenericTester<object, TValue> CreateGenericTesterFor<TValue>() => CreateGenericTesterFor<object, TValue>();

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        protected GenericTester<TEntryPoint, TValue> CreateGenericTesterFor<TEntryPoint, TValue>() where TEntryPoint : class => new(Output);

        /// <summary>
        /// Gets the <see cref="Internal.ObjectComparer"/>.
        /// </summary>
        protected ObjectComparer ObjectComparer => new(new XunitTestImplementor(Output));
    }
}