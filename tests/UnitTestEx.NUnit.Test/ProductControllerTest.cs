using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ProductControllerTest
    {
        [Test]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys/"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();
        }

        [Test]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("abc"))
                .AssertOK()
                .Assert(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void ServiceProvider()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = ApiTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = hc.GetAsync("test").Result;
            Assert.IsNotNull(r);
            Assert.AreEqual("test output", r.Content.ReadAsStringAsync().Result);
        }
    }
}