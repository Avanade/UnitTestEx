using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Function;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class ServiceBusEmulationTest : UnitTestBase
    {
        public ServiceBusEmulationTest(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task MessagePeek()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }, m => m.Subject = "Peek-a-boo").ConfigureAwait(false);

                    var m = await em.PeekAsync().ConfigureAwait(false);
                    Assert.NotNull(m);
                    Assert.Equal("Peek-a-boo", m.Subject);
                });
        }

        [Fact]
        public async Task ServiceBusReceiver2_Success()
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
            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageCompleted();
                });
        }

        [Fact]
        public void ServiceBusReceiver2_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Emulate(nameof(ServiceBusFunction.Run2), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertException<InvalidOperationException>("First name is required.").AssertMessageAbandoned();
                });
        }

        [Fact]
        public async Task ServiceBusReceiver3_Success()
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
            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "Bob", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageCompleted();
                });
        }

        [Fact]
        public async Task ServiceBusReceiver3_ValidationError()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertSuccess().AssertMessageDeadlettered("Validation error.");
                });
        }

        [Fact]
        public async Task ServiceBusReceiver3_UnhandledException()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true);
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

            await test.ServiceBusTrigger<ServiceBusFunction>()
                .EmulateAsync(nameof(ServiceBusFunction.Run3), async em =>
                {
                    await em.ClearAsync().ConfigureAwait(false);
                    await em.SendValueAsync(new Person { FirstName = "zerodivision", LastName = "Smith" }).ConfigureAwait(false);
                    var r = await em.RunAsync().ConfigureAwait(false);
                    r.AssertException<DivideByZeroException>("Divide by zero is not a thing.").AssertMessageAbandoned();
                });
        }

        [Fact]
        public async Task ServiceBusReceiver4_Success()
        {
            // Set up test, and only run where the 'ServiceBusConnectionString' has a value.
            using var test = CreateFunctionTester<Startup>(includeUserSecrets: true, additionalConfiguration: new KeyValuePair<string, string>("Run4QueueName", "unittestex"));
            var sbcs = test.Services.GetService<IConfiguration>().GetValue<string>("ServiceBusConnectionString");
            Skip.If(sbcs == null, "ServiceBusConnectionString configuration not set and therefore test cannot function.");

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