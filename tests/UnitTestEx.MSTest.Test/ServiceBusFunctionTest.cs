using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using UnitTestEx.Function;
using UnitTestEx.Json;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ServiceBusFunctionTest
    {
        [TestMethod]
        public void Object_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = "Smith" }, test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [TestMethod]
        public void Object_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonComparerOptions(new JsonElementComparerOptions { NullComparison = JsonElementComparison.Semantic });
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            var r = test.ReplaceHttpClientFactory(mcf)
                .Type<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = (string)null }, test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void Object_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = null, LastName = "Smith" }, test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceBusMessage_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceBusMessage_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonSerializer(new Json.JsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = (string)null }), test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceBusMessage_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = null, LastName = "Smith" }), test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceProvider()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = FunctionTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = hc.GetAsync("test").Result;
            Assert.IsNotNull(r);
            Assert.AreEqual("test output", r.Content.ReadAsStringAsync().Result);
        }

        [TestMethod]
        public void CreateServiceBusMessage()
        {
            using var test = FunctionTester.Create<Startup>();
            var sbrm = test.CreateServiceBusMessage(new ServiceBusMessage() { Subject = "xxx" });
            Assert.IsNotNull(sbrm);
            Assert.IsNotNull(sbrm.Subject);
            Assert.AreEqual("xxx", sbrm.Subject);
        }
    }
}