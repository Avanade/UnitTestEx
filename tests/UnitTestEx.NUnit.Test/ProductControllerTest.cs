using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ProductControllerTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var uti = NUnit.Internal.NUnitTestImplementor.Create();
            uti.WriteLine("ONE-TIME-SETUP");
            System.Diagnostics.Debug.WriteLine("ONE-TIME-SETUP");
        }

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
                .UseSetUp(new TestSetUp { ExpectedEventsEnabled = true })
                .Controller<ProductController>()
                .ExpectEvent("/test/product/*", "test.product.*c", "*")
                .Run(c => c.Get("abc"))
                .AssertOK()
                .Assert(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/xyz").Respond.WithJson(new { id = "Xyz", description = "Xtra yellow elephant" });

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .UseExpectedEvents()
                .Controller<ProductController>()
                .ExpectDestinationEvent("test-queue", "/test/product/*", "test.product.*z", "*")
                .ExpectDestinationEvent("test-queue2", "/test/*/xyz", "test.*", "*")
                .Run(c => c.Get("xyz"))
                .AssertOK()
                .Assert(new { id = "Xyz", description = "Xtra yellow elephant" });
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

        [Test]
        public void Configuration()
        {
            using var test = ApiTester.Create<Startup>();
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.AreEqual("VerySpecialValue", cv);
            
            cv = test.Configuration.GetValue<string>("OtherKey");
            Assert.AreEqual("OtherValue", cv);
        }

        [Test]
        public void DefaultHttpClient()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateDefaultClient(new Uri("https://someothersys"))
                .Request(HttpMethod.Get, "products/default").Respond.WithJson(new { id = "Def", description = "Default" });

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get())
                .AssertOK()
                .Assert(new { id = "Def", description = "Default" });
        }
    }
}