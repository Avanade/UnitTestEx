// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Hosting;
using CoreEx.Validation;
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
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        /// <returns>The <see cref="FunctionTester{TEntryPoint}"/>.</returns>
        protected FunctionTester<TEntryPoint> CreateFunctionTester<TEntryPoint>(bool? includeUnitTestConfiguration = null, bool? includeUserSecrets = null, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration = null)
            where TEntryPoint : FunctionsStartup, new()
            => new(Output, includeUnitTestConfiguration, includeUserSecrets, additionalConfiguration);

        /// <summary>
        /// Provides the <b>Xunit</b> <see cref="IValidator"/> testing capability.
        /// </summary>
        /// <param name="userName">The user name (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.UserName"/> where configured).</param>
        /// <returns>The <see cref="ValidationTester{TEntryPoint}"/>.</returns>
        protected ValidationTester<HostStartup> CreateValidationTester(string? userName = null) => CreateValidationTester<HostStartup>(userName);

        /// <summary>
        /// Provides the <b>Xunit</b> <see cref="IValidator"/> testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
        /// <param name="userName">The user name (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.UserName"/> where configured).</param>
        /// <returns>The <see cref="ValidationTester{TEntryPoint}"/>.</returns>
        protected ValidationTester<TEntryPoint> CreateValidationTester<TEntryPoint>(string? userName = null) where TEntryPoint : IHostStartup, new()
        {
            var vt = new ValidationTester<TEntryPoint>(Output);
            vt.UseUser(userName);
            return vt;
        }

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        protected GenericTester<HostStartup> CreateGenericTester(string? userName = null) => CreateGenericTester<HostStartup>(userName);

        /// <summary>
        /// Provides the <b>Xunit</b> generic testing capability.
        /// </summary>
        /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="GenericTester{TEntryPoint}"/>.</returns>
        protected GenericTester<TEntryPoint> CreateGenericTester<TEntryPoint>(string? userName = null) where TEntryPoint : IHostStartup, new()
        {
            var gt = new GenericTester<TEntryPoint>(Output);
            gt.UseUser(userName);
            return gt;
        }

        /// <summary>
        /// Gets the <see cref="Internal.ObjectComparer"/>.
        /// </summary>
        protected ObjectComparer ObjectComparer => new(new XunitTestImplementor(Output));
    }
}