using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnitTestEx;
using UnitTestEx.Abstractions;
using Xunit;
using Xunit.Sdk;

namespace UnitTestEx.Xunit3.Test
{
    /// <summary>
    /// Verifies xUnit v3 runtime framework integration.
    /// </summary>
    public sealed class RuntimeFrameworkTests(ITestOutputHelper output)
    {
        /// <summary>
        /// Verifies automatic detection, native equality assertions, and captured output.
        /// </summary>
        [Fact]
        public void AutomaticallyDetectsXunit3AndUsesNativeAssertions()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = null;
                var implementor = TestFrameworkImplementor.CreateForCurrentFramework(output);
                implementor.AssertAreEqual(new[] { 1, 2 }, new[] { 1, 2 });
                Assert.Throws<XunitException>(() => implementor.AssertFail("xUnit v3 auto-detection"));
                implementor.WriteLine("UNITTESTEX_XUNIT3_OUTPUT");
                using var loggerProvider = implementor.CreateLoggerProvider();
                var logger = loggerProvider.CreateLogger(nameof(RuntimeFrameworkTests));
                using var scope = logger.BeginScope(new Dictionary<string, object> { { "Framework", "xUnit v3" } });
                logger.LogInformation("UNITTESTEX_XUNIT3_LOG{NewLine}SECOND_LINE", Environment.NewLine);
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }

        /// <summary>
        /// Verifies explicit xUnit v3 selection.
        /// </summary>
        [Fact]
        public void ExplicitlySelectsXunit3()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = TestFramework.Xunit3;
                var implementor = TestFrameworkImplementor.CreateForCurrentFramework(output);
                Assert.Throws<XunitException>(() => implementor.AssertFail("xUnit v3 explicit selection"));
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }

        /// <summary>
        /// Verifies framework selection through the UnitTestEx environment configuration key.
        /// </summary>
        [Fact]
        public void ReadsExplicitSelectionFromEnvironment()
        {
            var previous = TestSetUp.TestFramework;
            var previousSetting = Environment.GetEnvironmentVariable("UnitTestEx__TestFramework");
            try
            {
                TestSetUp.TestFramework = null;
                Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", "xunit3");
                var implementor = TestFrameworkImplementor.CreateForCurrentFramework(output);
                Assert.Throws<XunitException>(() => implementor.AssertFail("xUnit v3 environment selection"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", previousSetting);
                TestSetUp.TestFramework = previous;
            }
        }
    }
}
