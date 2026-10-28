using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.IsolatedFunction;
using UnitTestEx.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class ProductFunctionTest : UnitTestBase
    {
        public ProductFunctionTest(ITestOutputHelper output) : base(output) { }

        [Fact]
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

        [Fact]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
                .ToActionResultAssertor()
                    .AssertOK()
                    .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public void Exception()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<ProductFunction>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/exception"), "exception"))
                .AssertException<InvalidOperationException>("An unexpected exception occured.");
        }
    }
}