using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using UnitTestEx;
using UnitTestEx.Abstractions;

[assembly: DoNotParallelize]

namespace UnitTestEx.MSTest4.Test
{
    /// <summary>
    /// Verifies MSTest 4 runtime framework integration.
    /// </summary>
    [TestClass]
    public sealed class RuntimeFrameworkTests
    {
        /// <summary>
        /// Verifies automatic detection, native equality assertions, and captured output.
        /// </summary>
        [TestMethod]
        public void AutomaticallyDetectsMSTest4AndUsesNativeAssertions()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = null;
                var implementor = TestFrameworkImplementor.Create();
                implementor.AssertAreEqual(2, 2);
                AssertThrows<AssertFailedException>(() => implementor.AssertFail("MSTest 4 auto-detection"));
                implementor.WriteLine("UNITTESTEX_MSTEST4_OUTPUT");
                using var loggerProvider = implementor.CreateLoggerProvider();
                var logger = loggerProvider.CreateLogger(nameof(RuntimeFrameworkTests));
                using var scope = logger.BeginScope(new Dictionary<string, object> { { "Framework", "MSTest 4" } });
                logger.LogInformation("UNITTESTEX_MSTEST4_LOG{NewLine}SECOND_LINE", Environment.NewLine);
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }

        /// <summary>
        /// Verifies explicit MSTest 4 selection.
        /// </summary>
        [TestMethod]
        public void ExplicitlySelectsMSTest4()
        {
            var previous = TestSetUp.TestFramework;
            try
            {
                TestSetUp.TestFramework = TestFramework.MSTest4;
                var implementor = TestFrameworkImplementor.Create();
                AssertThrows<AssertFailedException>(() => implementor.AssertFail("MSTest 4 explicit selection"));
            }
            finally
            {
                TestSetUp.TestFramework = previous;
            }
        }

        /// <summary>
        /// Verifies that an action throws the expected exception type.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="action">The action to invoke.</param>
        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new Exception($"Expected '{typeof(TException).FullName}' to be thrown.");
        }
    }
}
