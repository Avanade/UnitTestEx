using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnitTestEx;
using UnitTestEx.Abstractions;

namespace UnitTestEx.NUnit5.Test
{
    /// <summary>
    /// Verifies NUnit 5 runtime framework integration.
    /// </summary>
    [TestFixture]
    public sealed class RuntimeFrameworkTests
    {
        /// <summary>
        /// Verifies automatic detection, native equality assertions, and captured output.
        /// </summary>
        [Test]
        public void AutomaticallyDetectsNUnit5AndUsesNativeAssertions()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = null;
                var implementor = TestFrameworkImplementor.Create();
                implementor.AssertAreEqual(new[] { 1, 2 }, new[] { 1, 2 });
                Assert.Throws<AssertionException>(() => implementor.AssertFail("NUnit 5 auto-detection"));
                implementor.WriteLine("UNITTESTEX_NUNIT5_OUTPUT");
                using var loggerProvider = implementor.CreateLoggerProvider();
                var logger = loggerProvider.CreateLogger(nameof(RuntimeFrameworkTests));
                using var scope = logger.BeginScope(new Dictionary<string, object> { { "Framework", "NUnit 5" } });
                logger.LogInformation("UNITTESTEX_NUNIT5_LOG{NewLine}SECOND_LINE", Environment.NewLine);
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }

        /// <summary>
        /// Verifies explicit NUnit 5 selection.
        /// </summary>
        [Test]
        public void ExplicitlySelectsNUnit5()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = TestFramework.NUnit5;
                var implementor = TestFrameworkImplementor.Create();
                Assert.Throws<AssertionException>(() => implementor.AssertFail("NUnit 5 explicit selection"));
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }
    }
}
