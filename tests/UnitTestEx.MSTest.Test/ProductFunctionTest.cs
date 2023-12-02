using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ProductFunctionTest
    {
        [TestMethod]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/xyz"), "xyz", test.Logger))
                .AssertNotFound();
        }

        [TestMethod]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc", test.Logger))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [TestMethod]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc", test.Logger))
                .ToActionResultAssertor()
                    .AssertOK()
                    .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [TestMethod]
        public void Exception()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/exception"), "exception", test.Logger))
                .AssertException<InvalidOperationException>("An unexpected exception occured.");
        }
    }
}