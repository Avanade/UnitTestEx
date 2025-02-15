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
    public class ProductControllerTest : WithApiTester<Startup>
    {
        public ProductControllerTest(ApiTestFixture<Startup> fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys/"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            Test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();
        }

        [Fact]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            Test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("abc"))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Fact]
        public async Task ServiceProvider()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            var hc = Test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = await hc.GetAsync("test");
            Assert.NotNull(r);
            Assert.Equal("test output", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public void To_HttpResponseMessage_Created()
        {
            Test.Type<ProductController>()
                .Run(c => c.GetCreated())
                .ToHttpResponseMessageAssertor()
                .AssertCreated()
                .AssertLocationHeaderContains("bananas")
                .AssertContent("abc");
        }

        [Fact]
        public void To_HttpResponseMessage_OK()
        {
            Test.Type<ProductController>()
                .Run(c => c.GetOK())
                .ToHttpResponseMessageAssertor()
                .AssertOK();
        }
    }
}