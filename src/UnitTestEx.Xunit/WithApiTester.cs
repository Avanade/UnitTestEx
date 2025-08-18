// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.AspNetCore;
using UnitTestEx.Xunit.Internal;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable IDE0130 // Namespace does not match folder structure; improves usability.
namespace UnitTestEx
#pragma warning restore IDE0130
{
    /// <summary>
    /// Provides a shared <see cref="Test"/> <see cref="ApiTester{TEntryPoint}"/> to enable usage of the same underlying <see cref="ApiTesterBase{TEntryPoint, TSelf}.GetTestServer"/> instance across multiple tests.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <remarks>Implements <see cref="IDisposable"/> to automatically dispose of the <see cref="Test"/> instance to release all resources.
    /// <para>Be aware that using may result in cross-test contamination.</para></remarks>
    public abstract class WithApiTester<TEntryPoint> : UnitTestBase, IClassFixture<ApiTestFixture<TEntryPoint>> where TEntryPoint : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WithApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="fixture">The shared <see cref="ApiTestFixture{TEntryPoint}"/>.</param>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        public WithApiTester(ApiTestFixture<TEntryPoint> fixture, ITestOutputHelper output) : base(output)
        {
            Test = fixture.Test;
            XunitLocalTestImplementor.SetLocalImplementor(new XunitTestImplementor(output));
        }

        /// <summary>
        /// Gets the shared <see cref="ApiTester{TEntryPoint}"/> for testing.
        /// </summary>
        public ApiTester<TEntryPoint> Test { get; }
    }
}