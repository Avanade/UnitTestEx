using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
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
            test.ReplaceHttpClientFactory(mcf)
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
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("abc"))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public async Task ServiceProvider()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = CreateApiTester<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = await hc.GetAsync("test");
            Assert.NotNull(r);
            Assert.Equal("test output", await r.Content.ReadAsStringAsync());
        }
    }
}