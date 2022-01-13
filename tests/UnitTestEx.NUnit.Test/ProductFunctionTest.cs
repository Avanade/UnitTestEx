using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;
using UnitTestEx.NUnit;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ProductFunctionTest
    {
        [Test]
        public void Notfound()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            using var test = FunctionTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/xyz", null), "xyz", test.Logger))
                .AssertNotFound();
        }

        [Test]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc", null), "abc", test.Logger))
                .AssertOK(new { id = "Abc", description = "A blue carrot" });
        }
    }
}