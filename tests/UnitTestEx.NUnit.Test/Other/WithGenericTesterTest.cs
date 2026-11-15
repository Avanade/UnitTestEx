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
            var outer = TestFrameworkImplementor.Create();
            var implementor = new NullTestImplementor();
            var fixture = new LifecycleGenericTester(() => implementor, throwOnDispose: true);

            Assert.Throws<InvalidOperationException>(fixture.Dispose);
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
            Assert.That(TestFrameworkImplementor.Create(), Is.SameAs(outer));
            Assert.DoesNotThrow(fixture.Dispose);
        }

        /// <summary>Verifies nested fixtures restore the enclosing context after disposal.</summary>
        [Test]
        public void NestedFixtures_RestoreTheOuterImplementor()
        {
            var previous = TestFrameworkImplementor.Create();
            var outer = new NullTestImplementor();
            var inner = new NullTestImplementor();
            using (var outerFixture = new LifecycleGenericTester(() => outer))
            {
                Assert.That(outerFixture.Test.Implementor, Is.SameAs(outer));
                using (var innerFixture = new LifecycleGenericTester(() => inner))
                    Assert.That(innerFixture.Test.Implementor, Is.SameAs(inner));

                Assert.That(TestFrameworkImplementor.Create(), Is.SameAs(outer));
            }

            Assert.That(TestFrameworkImplementor.Create(), Is.SameAs(previous));
        }

        /// <summary>Verifies a failed fixture initialization leaves the enclosing context intact.</summary>
        [Test]
        public void FailedFixtureConstruction_PreservesOuterContext()
        {
            var outer = TestFrameworkImplementor.Create();
            Assert.Throws<InvalidOperationException>(() => new LifecycleGenericTester(() => null!));
            Assert.That(TestFrameworkImplementor.Create(), Is.SameAs(outer));
        }

        /// <summary>Verifies registered local factories are used for fresh fixture contexts.</summary>
        [Test]
        public void RegisteredLocalFactory_IsUsedForNestedFixtures()
        {
            var factoryImplementor = new NullTestImplementor();
            TestFrameworkImplementor.SetLocalCreateFactory(() => factoryImplementor);
            try
            {
                using var fixture = new LifecycleGenericTester();
                Assert.That(fixture.Test.Implementor, Is.SameAs(factoryImplementor));
                using var direct = GenericTester.Create(output: new System.IO.StringWriter());
                Assert.That(direct.Implementor, Is.SameAs(factoryImplementor));
            }
            finally
            {
                TestFrameworkImplementor.ResetLocalCreateFactory();
            }
        }

        /// <summary>Verifies NUnit records native failures without aborting multiple-assert scopes.</summary>
        [Test]
        public void AssertFail_RespectsMultipleAssertionScopes()
        {
            var continued = false;
            var failure = Assert.Throws<MultipleAssertException>(() => Assert.Multiple(() =>
            {
                TestFrameworkImplementor.CreateForCurrentFramework().AssertFail("recorded failure");
                continued = true;
            }));

            Assert.That(continued, Is.True);
            Assert.That(failure!.Message, Does.Contain("recorded failure"));
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