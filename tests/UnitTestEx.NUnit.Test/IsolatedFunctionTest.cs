using NUnit.Framework;
using System.Net.Http;
using UnitTestEx.Expectations;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class IsolatedFunctionTest
    {
        [Test]
        public void HttpTrigger()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<HttpFunction>()
                .ExpectLogContains("C# HTTP trigger function processed a request.")
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "hello")))
                .AssertSuccess();
        }
    }
}