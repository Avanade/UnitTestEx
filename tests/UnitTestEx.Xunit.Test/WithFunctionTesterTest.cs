using Moq;
using System.Net.Http;
using UnitTestEx.IsolatedFunction;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Xunit.Test
{
    /// <summary>
    /// Verifies the framework-neutral FunctionTester fixture base.
    /// </summary>
    public class WithFunctionTesterTest : WithFunctionTester<HttpFunction>
    {
        /// <summary>
        /// Tracks configuration-hook invocations.
        /// </summary>
        private int _configurationCount;

        /// <summary>
        /// Tracks disposal-hook invocations.
        /// </summary>
        private int _disposeCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithFunctionTesterTest"/> class.
        /// </summary>
        /// <param name="output">The xUnit test output helper.</param>
        public WithFunctionTesterTest(ITestOutputHelper output) : base(output: output) { }

        /// <summary>
        /// Verifies configuration and disposal hooks are lazy, re-entrant, and invoked once.
        /// </summary>
        [Fact]
        public void ConfigurationAndDisposalHooks_RunOnce()
        {
            Assert.Equal(0, _configurationCount);
            var tester = Test;

            Assert.Same(tester, Test);
            Assert.Equal(1, _configurationCount);
            Dispose();
            Assert.Equal(1, _disposeCount);
            Assert.Throws<System.ObjectDisposedException>(() => _ = Test);
        }

        /// <summary>
        /// Verifies that tester assertions and output use the current xUnit test.
        /// </summary>
        [Fact]
        public void OutputObject_UsesXunitAssertionsAndCapturedOutput()
        {
            var output = new Mock<ITestOutputHelper>();
            using var fixture = new FunctionFixture(output.Object);

            var result = fixture.Test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(fixture.Test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK();

            Assert.Throws<XunitException>(() => result.AssertValue("wrong value"));
            output.Verify(o => o.WriteLine(It.IsAny<string>()), Times.AtLeastOnce());
        }

        /// <summary>
        /// Verifies that a derived test class uses detected xUnit assertions.
        /// </summary>
        [Fact]
        public void DerivedTestClass_UsesDetectedXunitAssertions()
        {
            Test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(Test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK()
                .AssertValue("Welcome to Azure Functions!");
        }

        /// <inheritdoc/>
        protected override void OnConfiguration()
        {
            _configurationCount++;
            _ = Test;
        }

        /// <inheritdoc/>
        protected override void OnDispose() => _disposeCount++;

        /// <summary>
        /// Provides a tester bound to a specific test output helper.
        /// </summary>
        private sealed class FunctionFixture(ITestOutputHelper output)
            : WithFunctionTester<HttpFunction>(output: output) { }
    }
}
