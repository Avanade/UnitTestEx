using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ProductControllerTest
    {
        public ProductControllerTest()
        {
            TestSetUp.Default.RegisterAutoSetUp((_, __, ___) =>
            {
                return Task.FromResult((true, "Some real magic happened here!"));
            });
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var uti = NUnit.Internal.NUnitTestImplementor.Create();
            uti.WriteLine("ONE-TIME-SETUP"); // This does not work; bug of test framework.
            System.Diagnostics.Debug.WriteLine("ONE-TIME-SETUP");
            TestSetUp.Default.SetUp();
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
                .Controller<ProductController>()
                .ExpectLogContains("Received HTTP response OK")
                .Run(c => c.Get("abc"))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Get, "products/xyz").Respond.WithJson(new { id = "Xyz", description = "Xtra yellow elephant" });

            using var test = ApiTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Controller<ProductController>()
                .Run(c => c.Get("xyz"))
                .AssertOK()
                .AssertValue(new { id = "Xyz", description = "Xtra yellow elephant" });
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
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Content.ReadAsStringAsync().Result, Is.EqualTo("test output"));
        }

        [Test]
        public void Configuration()
        {
            using var test = ApiTester.Create<Startup>();
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.That(cv, Is.EqualTo("VerySpecialValue"));
            
            cv = test.Configuration.GetValue<string>("OtherKey");
            Assert.That(cv, Is.EqualTo("OtherValue"));
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
                .AssertValue(new { id = "Def", description = "Default" });
        }
    }
}