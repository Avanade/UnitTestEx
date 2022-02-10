using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using UnitTestEx.Function;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class ServiceBusEmulationTest : UnitTestBase
    {
        public ServiceBusEmulationTest(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void MessagePeek()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), em =>
                {
                    var m = em.Clear()
                        .SendValue(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "Peek-a-boo")
                        .Peek();

                    Assert.NotNull(m);
                    Assert.Equal("Peek-a-boo", m.Subject);
                });
        }

        [SkippableFact]
        public void ServiceBusReceiver2_Success()
        {
            // Mock the downstream http client.
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true).ConfigureServices(sc => mcf.Replace(sc));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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

        [SkippableFact]
        public void ServiceBusReceiver2_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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

        [SkippableFact]
        public void ServiceBusReceiver3_Success()
        {
            // Mock the downstream http client.
            var mcf = CreateMockHttpClientFactory();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true).ConfigureServices(sc => mcf.Replace(sc));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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

        [SkippableFact]
        public void ServiceBusReceiver3_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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

        [SkippableFact]
        public void ServiceBusReceiver3_UnhandledException()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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

        [SkippableFact]
        public void ServiceBusReceiver4_Success()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true, additionalConfiguration: new KeyValuePair<string, string>("Run4QueueName", "unittestex"));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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