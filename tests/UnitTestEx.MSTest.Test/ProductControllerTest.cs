using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.MSUnit;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ProductControllerTest
    {
        [TestMethod]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys/"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = ApiTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();
        }

        [TestMethod]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = ApiTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .Controller<ProductController>()
                .Run(c => c.Get("abc"))
                .AssertOK(new { id = "Abc", description = "A blue carrot" });
        }
    }
}