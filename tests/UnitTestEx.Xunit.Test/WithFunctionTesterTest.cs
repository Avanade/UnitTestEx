using Moq;
using System.Net.Http;
using UnitTestEx.Abstractions;
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

        /// <summary>Verifies explicit output is honored inside an automatically detected ambient fixture.</summary>
        [Fact]
        public void DirectTester_UsesExplicitOutputInsideFixture()
        {
            var output = new Mock<ITestOutputHelper>();
            using var tester = GenericTester.Create(output.Object);
            tester.Implementor.WriteLine("explicit output");
            output.Verify(o => o.WriteLine("explicit output"), Times.Once());
        }

        /// <summary>Verifies generic testers use the output or factory installed by their base constructor.</summary>
        [Fact]
        public void GenericFixture_UsesItsOutputAndFactory()
        {
            var output = new Mock<ITestOutputHelper>();
            using (var fixture = new GenericFixture(output.Object))
            {
                fixture.Test.Implementor.WriteLine("fixture output");
                output.Verify(o => o.WriteLine("fixture output"), Times.Once());
            }

            var implementor = new NullTestImplementor();
            using var customFixture = new GenericFixture(createFactory: () => implementor);
            Assert.Same(implementor, customFixture.Test.Implementor);
            using var direct = GenericTester.Create(output.Object);
            Assert.Same(implementor, direct.Implementor);
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

        /// <summary>Provides a generic fixture for checking its installed implementor.</summary>
        private sealed class GenericFixture(object output = null, System.Func<TestFrameworkImplementor> createFactory = null)
            : WithGenericTester<object>(output, createFactory) { }
    }
}
