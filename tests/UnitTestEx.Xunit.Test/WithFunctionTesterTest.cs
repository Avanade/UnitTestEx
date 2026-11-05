using Moq;
using System.Net.Http;
using UnitTestEx.IsolatedFunction;
using UnitTestEx.Xunit.Internal;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UnitTestEx.Xunit.Test
{
    public class WithFunctionTesterTest : WithFunctionTester<HttpFunction>
    {
        public WithFunctionTesterTest(ITestOutputHelper output) : base(() => XunitTestImplementor.Create(output)) { }

        [Fact]
        public void ExplicitFactory_UsesXunitAssertionsAndCapturedOutput()
        {
            var output = new Mock<ITestOutputHelper>();
            using var fixture = new FunctionFixture(output.Object);
            Assert.IsType<XunitTestImplementor>(fixture.Test.Implementor);

            var result = fixture.Test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(fixture.Test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK();

            Assert.Throws<XunitException>(() => result.AssertValue("wrong value"));
            output.Verify(o => o.WriteLine("{0}", It.IsAny<object[]>()), Times.AtLeastOnce());
        }

        [Fact]
        public void DerivedTestClass_InvokesFunctionWithXunitImplementor()
        {
            Assert.IsType<XunitTestImplementor>(Test.Implementor);
            Test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(Test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK()
                .AssertValue("Welcome to Azure Functions!");
        }

        private sealed class FunctionFixture(ITestOutputHelper output)
            : WithFunctionTester<HttpFunction>(() => XunitTestImplementor.Create(output)) { }
    }
}
