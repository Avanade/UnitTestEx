using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ProductControllerTest
    {
        [TestMethod]
        public void Notfound_WithoutConfigurations()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            Startup.MessageProcessingHandler.WasExecuted = false;

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();

            Assert.IsFalse(Startup.MessageProcessingHandler.WasExecuted);
        }

        [TestMethod]
        public void Notfound_WithConfigurations()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX").WithConfigurations()
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            Startup.MessageProcessingHandler.WasExecuted = false;

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertNotFound();

            Assert.IsTrue(Startup.MessageProcessingHandler.WasExecuted);
        }

        [TestMethod]
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
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [TestMethod]
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

        [TestMethod]
        public void MockHttpClientFactory_NoMocking()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX").WithoutMocking();

            Startup.MessageProcessingHandler.WasExecuted = false;

            using var test = ApiTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var ex = Assert.ThrowsException<AggregateException>(() => hc.GetAsync("test").Result);
            Assert.AreEqual("One or more errors occurred. (No such host is known. (somesys:443))", ex.Message);

            Assert.IsTrue(Startup.MessageProcessingHandler.WasExecuted);

            mcf.VerifyAll();
        }

        [TestMethod]
        public void MockHttpClientFactory_NoMocking_Exclude()
        {
            using var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX").WithoutMocking(typeof(Startup.MessageProcessingHandler));

            Startup.MessageProcessingHandler.WasExecuted = false;

            using var test = ApiTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var ex = Assert.ThrowsException<AggregateException>(() => hc.GetAsync("test").Result);
            Assert.AreEqual("One or more errors occurred. (No such host is known. (somesys:443))", ex.Message);

            Assert.IsFalse(Startup.MessageProcessingHandler.WasExecuted);

            mcf.VerifyAll();
        }
    }
}