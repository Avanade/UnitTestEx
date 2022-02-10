using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ServiceBusEmulationTest
    {
        [TestMethod]
        public void MessagePeek()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), em =>
                {
                    var m = em.Clear()
                        .SendValue(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "Peek-a-boo")
                        .Peek();

                    Assert.IsNotNull(m);
                    Assert.AreEqual("Peek-a-boo", m.Subject);
                });
        }

        [TestMethod]
        public void ServiceBusReceiver2_Success()
        {
            // Mock the downstream http client.
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true).ConfigureServices(sc => mcf.Replace(sc));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            // Run the test by emulating the trigger initiation from Azure Service Bus.
            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), em =>
                {
                    em.Clear()
                        .SendValue(new Person { FirstName = "Bob", LastName = "Smith" })
                        .Run()
                        .AssertSuccess()
                        .AssertMessageCompleted();
                });
        }

        [TestMethod]
        public void ServiceBusReceiver2_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), em =>
                {
                    em.Clear()
                        .SendValue(new Person { LastName = "Smith" })
                        .Run()
                        .AssertException<InvalidOperationException>("First name is required.")
                        .AssertMessageAbandoned();
                });
        }

        [TestMethod]
        public void ServiceBusReceiver3_Success()
        {
            // Mock the downstream http client.
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true).ConfigureServices(sc => mcf.Replace(sc));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            // Run the test by emulating the trigger initiation from Azure Service Bus.
            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run3), em =>
                {
                    em.Clear()
                        .SendValue(new Person { FirstName = "Bob", LastName = "Smith" })
                        .Run()
                        .AssertSuccess()
                        .AssertMessageCompleted();
                });
        }

        [TestMethod]
        public void ServiceBusReceiver3_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run3), em =>
                {
                    em.Clear()
                        .SendValue(new Person { LastName = "Smith" })
                        .Run()
                        .AssertSuccess()
                        .AssertMessageDeadlettered("Validation error.");
                });
        }

        [TestMethod]
        public void ServiceBusReceiver3_UnhandledException()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run3), em =>
                {
                    em.Clear()
                        .SendValue(new Person { FirstName = "zerodivision", LastName = "Smith" })
                        .Run()
                        .AssertException<DivideByZeroException>("Divide by zero is not a thing.")
                        .AssertMessageAbandoned();
                });
        }

        [TestMethod]
        public void ServiceBusReceiver4_Success()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true, additionalConfiguration: new KeyValuePair<string, string>("Run4QueueName", "unittestex"));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            // Run the test by emulating the trigger initiation from Azure Service Bus.
            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run4), em =>
                {
                    em.Clear()
                        .SendValue(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "RUN-FOUR")
                        .Run()
                        .AssertSuccess()
                        .AssertMessageCompleted();
                });
        }
    }
}