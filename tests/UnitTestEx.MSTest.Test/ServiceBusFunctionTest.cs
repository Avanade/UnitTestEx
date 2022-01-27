using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;
using UnitTestEx.Mocking;

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
            test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = "Smith" }, test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [TestMethod]
        public void Object_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            var r = test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = "Smith" }, test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void Object_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
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
            test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessage(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceBusMessage_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            var r = test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessage(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [TestMethod]
        public void ServiceBusMessage_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ConfigureServices(sc => mcf.Replace(sc))
                .GenericTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessage(new Person { FirstName = null, LastName = "Smith" }), test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }
    }
}