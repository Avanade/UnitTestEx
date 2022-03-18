using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;
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
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = CreateFunctionTester<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/xyz"), "xyz", test.Logger))
                .AssertNotFound();
        }

        [Fact]
        public void Success()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = CreateFunctionTester<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc", test.Logger))
                .AssertOK()
                .Assert(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public void Success2()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = CreateFunctionTester<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc", test.Logger))
                .ToActionResultAssertor()
                    .AssertOK()
                    .Assert(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public void Exception()
        {
            var mcf = CreateMockHttpClientFactory();

            using var test = CreateFunctionTester<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/exception"), "exception", test.Logger))
                .AssertException<InvalidOperationException>("An unexpected exception occured.");
        }
    }
}