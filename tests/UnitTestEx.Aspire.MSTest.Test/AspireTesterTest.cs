using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTestEx.Abstractions;
using UnitTestEx.Api.Models;

namespace UnitTestEx.Aspire.MSTest.Test
{
    [TestClass]
    public class AspireTesterTest
    {
        [TestMethod]
        public async Task Http_Get_ReturnsSuccess()
        {
            // Note: UnitTestEx.Api's Program.cs deliberately fails fast at host start up unless the 'SpecialKey' configuration is set; this is what WithResourceEnvironment is proving out.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            tester.Http("api")
                .Run(HttpMethod.Get, "Person?firstName=John&lastName=Doe")
                .AssertOK()
                .AssertContent("John-Doe-");
        }

        [TestMethod]
        public async Task WithResourceEnvironment_AppliedBeforeBuild()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            tester.Http("api")
                .Run(HttpMethod.Get, "Person/1")
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [TestMethod]
        public async Task BeforeStart_InvokedInOrder_BeforeAnyResourceStarts()
        {
            var order = new System.Collections.Generic.List<string>();

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(app =>
                {
                    Assert.IsNotNull(app);
                    order.Add("first");
                    return Task.CompletedTask;
                })
                .BeforeStart(app =>
                {
                    Assert.IsNotNull(app);
                    order.Add("second");
                    return Task.CompletedTask;
                });

            // Triggers build+start; both BeforeStart callbacks must already have run (in registration order) before this returns.
            await tester.WaitForResourceAsync("api");

            CollectionAssert.AreEqual(new[] { "first", "second" }, order);
        }

        [TestMethod]
        public async Task BeforeStart_Throws_AbortsStartUpAndPropagates()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(_ => throw new InvalidOperationException("Simulated pre-start failure."));

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => tester.WaitForResourceAsync("api"));
            Assert.AreEqual("Simulated pre-start failure.", ex.Message);
        }

        [TestMethod]
        public async Task BeforeStart_CanResolveConnectionStringResource()
        {
            string? connectionString = null;

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(async app => connectionString = await AspireTesterBase.GetConnectionStringAsync(app, "test-db"));

            // Triggers build+start; GetConnectionStringAsync must have already resolved the value above - Aspire's own testing extension
            // of the same name would throw here as the application has not yet started (see AspireTesterBase.GetConnectionStringAsync's remarks).
            await tester.WaitForResourceAsync("api");

            Assert.AreEqual("Data Source=unit-test;", connectionString);
        }

        [TestMethod]
        public async Task Checkpoint_And_Delay_AggregatesResourceLogs()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var spy = new SpyTestFrameworkImplementor(tester.Implementor);
            tester.ReplaceTestFrameworkImplementor(spy);

            var checkpointResult = tester.Checkpoint("Confirming Checkpoint() writes context for Aspire multi-host testers too.");

            // Fire a raw (uninstrumented) request against the 'api' resource part-way through the delay window to simulate genuine background/inter-resource activity that is not
            // tied to a tester-driven request/response (which would otherwise claim - and so report - the resource's log line itself, rather than Delay).
            var backgroundCallTask = Task.Run(async () =>
            {
                await Task.Delay(300);
                using var client = ((IHttpClientSource)tester).CreateHttpClient("api");
                using var response = await client.GetAsync("Person/1");
                response.EnsureSuccessStatusCode();
            });

            var sw = Stopwatch.StartNew();
            var delayResult = tester.Delay(TimeSpan.FromSeconds(2), "Waiting for a background Person lookup to complete and log.");
            sw.Stop();

            await backgroundCallTask;

            Assert.AreSame(tester, checkpointResult);
            Assert.AreSame(tester, delayResult);
            Assert.IsTrue(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.IsTrue(spy.Lines.Contains("CHECKPOINT >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("DELAY (00:00:02) >")));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Waiting for a background Person lookup to complete and log.")));
            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal)));
        }
    }
}

