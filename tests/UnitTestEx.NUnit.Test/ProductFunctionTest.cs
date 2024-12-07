using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Expectations;
using UnitTestEx.Function;

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
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/xyz"), "xyz", test.Logger))
                .AssertNotFound();
        }

        [Test]
        public void Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .ExpectLogContains("C# HTTP trigger function processed a request.")
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc", test.Logger))
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void Success2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc"), "abc", test.Logger))
                .ToActionResultAssertor()
                    .AssertOK()
                    .AssertValue(new { id = "Abc", description = "A blue carrot" });
        }

        [Test]
        public void Exception()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .HttpTrigger<ProductFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/exception"), "exception", test.Logger))
                .AssertException<InvalidOperationException>("An unexpected exception occured.");
        }

        [Test]
        public void TimerTriggered()
        {
            using var test = FunctionTester.Create<Startup>();
            test.Type<ProductFunction>()
                .ExpectLogContains("(DI)")
                .ExpectLogContains("(method)")
                .Run(f => f.DailyRun(new TimerInfo(new DailySchedule("2:00:00"), It.IsAny<ScheduleStatus>(), false), test.Logger))
                .AssertSuccess();
        }   
    }
}