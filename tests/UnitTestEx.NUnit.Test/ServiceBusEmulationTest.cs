using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Function;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    [NonParallelizable]
    public class ServiceBusEmulationTest
    {
        [Test]
        public async Task MessagePeek()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "Peek-a-boo").ConfigureAwait(false);

                    var m = await em.PeekAsync().ConfigureAwait(false);
                    Assert.IsNotNull(m);
                    Assert.AreEqual("Peek-a-boo", m.Subject);
                });
        }

        [Test]
        public async Task ServiceBusReceiver2_Success()
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
            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageCompleted();
                });
        }

        [Test]
        public void ServiceBusReceiver2_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertException<InvalidOperationException>("First name is required.").AssertMessageAbandoned();
                });
        }

        [Test]
        public async Task ServiceBusReceiver3_Success()
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
            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageCompleted();
                });
        }

        [Test]
        public async Task ServiceBusReceiver3_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageDeadlettered("Validation error.");
                });
        }

        [Test]
        public async Task ServiceBusReceiver3_UnhandledException()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "zerodivision", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertException<DivideByZeroException>("Divide by zero is not a thing.").AssertMessageAbandoned();
                });
        }

        [Test]
        public async Task ServiceBusReceiver4_Success()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = FunctionTester.Create<Startup>(includeUserSecrets: true, additionalConfiguration: new KeyValuePair<string, string>("Run4QueueName", "unittestex"));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            if (sbcs == null)
                Assert.Inconclusive("ServiceBusConnectionString configuration not set and therefore test cannot function.");

            // Run the test by emulating the trigger initiation from Azure Service Bus.
            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run4), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "RUN-FOUR").ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageCompleted();
                });
        }
    }
}