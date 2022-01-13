using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class ProductControllerTest : UnitTestBase
    {
        public ProductControllerTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Notfound()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys/"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = CreateApiTester<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();
        }

        [Fact]
        public void Success()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = CreateApiTester<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .Controller<ProductController>()
                .Run(c => c.Get("abc"))
                .AssertOK(new { id = "Abc", description = "A blue carrot" });
        }
    }
}