using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Xunit;
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
                .AssertOK()
                .Assert(new { id = "Abc", description = "A blue carrot" });
        }


        [Fact]
        public void ServiceProvider()
        {
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = CreateApiTester<Startup>();
            var hc = test.ConfigureServices(sc => mcf.Replace(sc))
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = hc.GetAsync("test").Result;
            Assert.NotNull(r);
            Assert.Equal("test output", r.Content.ReadAsStringAsync().Result);
        }
    }
}