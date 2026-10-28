using NUnit.Framework;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using UnitTestEx.Expectations;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ProductFunctionTest
    {
        [Test]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/xyz"), "xyz"))
                .AssertNotFound();
        }

        [Test]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<ProductFunction>();
            var assertor =test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .ExpectLogContains("C# HTTP trigger function processed a request.")
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });

            Assert.That(assertor.LogMessages.Count(m => m.Contains("C# HTTP trigger function processed a request.")), Is.EqualTo(1), "Expected log message was not found exactly once.");
        }

        [Test]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc"))
                .ToActionResultAssertor()
                    .AssertOK()
                    .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void Exception()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/exception"), "exception"))
                .AssertException<InvalidOperationException>("An unexpected exception occured.");
        }

        [Test]
        public void TimerTriggered()
        {
            using var test = FunctionTester.Create<ProductFunction>();
            test.Type<ProductFunction>()
                .ExpectLogContains("(DI)")
                .ExpectLogContains("(method)")
                .Run(f => f.DailyRun(default!))
                .AssertSuccess();
        }   
    }
}