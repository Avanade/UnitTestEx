#nullable enable

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnitTestEx.Abstractions;

namespace UnitTestEx.NUnit.Test.Other
{
    public class WithGenericTesterTest : WithGenericTester<EntryPoint>
    {
        [Test]
        public void Run_Success()
        {
            var c = Test.Services.GetService<IConfiguration>();

            Test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [Test]
        public void Run_Success_AssertJSON()
        {
            Test.Run(() => 1)
                .AssertSuccess()
                .AssertJson("1");
        }

        /// <summary>
        /// Verifies configuration is deferred until the tester is first accessed and runs once.
        /// </summary>
        [Test]
        public void OnConfiguration_RunsOnceWhenTestIsFirstAccessed()
        {
            var fixture = new LifecycleGenericTester();
            Assert.That(fixture.ConfigurationCount, Is.Zero);
            var tester = fixture.Test;

            Assert.That(fixture.Test, Is.SameAs(tester));
            Assert.That(fixture.ConfigurationCount, Is.EqualTo(1));
            fixture.Dispose();
        }

        /// <summary>
        /// Verifies the local implementor is reset even when a derived disposal hook throws.
        /// </summary>
        [Test]
        public void OnDispose_ResetsFrameworkContextWhenHookThrows()
        {
            var implementor = new NullTestImplementor();
            var fixture = new LifecycleGenericTester(() => implementor, throwOnDispose: true);

            Assert.Throws<InvalidOperationException>(fixture.Dispose);
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
            Assert.That(TestFrameworkImplementor.Create(), Is.Not.SameAs(implementor));
            Assert.DoesNotThrow(fixture.Dispose);
        }

        /// <summary>
        /// Provides lifecycle-hook coverage for the generic tester base.
        /// </summary>
        private sealed class LifecycleGenericTester(Func<TestFrameworkImplementor>? createFactory = null, bool throwOnDispose = false)
            : WithGenericTester<object>(createFactory: createFactory)
        {
            /// <summary>
            /// Gets the number of configuration-hook invocations.
            /// </summary>
            public int ConfigurationCount { get; private set; }

            /// <summary>
            /// Gets the number of disposal-hook invocations.
            /// </summary>
            public int DisposeCount { get; private set; }

            /// <inheritdoc/>
            protected override void OnConfiguration()
            {
                ConfigurationCount++;
                _ = Test;
            }

            /// <inheritdoc/>
            protected override void OnDispose()
            {
                DisposeCount++;
                if (throwOnDispose)
                    throw new InvalidOperationException("dispose hook failed");
            }
        }
    }
}