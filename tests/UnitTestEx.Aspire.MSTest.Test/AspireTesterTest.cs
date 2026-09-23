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
        public async Task Reason_And_Wait_AggregatesResourceLogs()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var spy = new SpyTestFrameworkImplementor(tester.Implementor);
            tester.ReplaceTestFrameworkImplementor(spy);

            var reasonResult = tester.Reason("Confirming Reason() writes context for Aspire multi-host testers too.");

            // Fire a raw (uninstrumented) request against the 'api' resource part-way through the wait window to simulate genuine background/inter-resource activity that is not
            // tied to a tester-driven request/response (which would otherwise claim - and so report - the resource's log line itself, rather than Wait).
            var backgroundCallTask = Task.Run(async () =>
            {
                await Task.Delay(300);
                using var client = ((IHttpClientSource)tester).CreateHttpClient("api");
                using var response = await client.GetAsync("Person/1");
                response.EnsureSuccessStatusCode();
            });

            var sw = Stopwatch.StartNew();
            var waitResult = tester.Wait("Waiting for a background Person lookup to complete and log.", TimeSpan.FromSeconds(2));
            sw.Stop();

            await backgroundCallTask;

            Assert.AreSame(tester, reasonResult);
            Assert.AreSame(tester, waitResult);
            Assert.IsTrue(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.IsTrue(spy.Lines.Contains("REASON >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("WAIT (00:00:02) >")));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Waiting for a background Person lookup to complete and log.")));
            Assert.IsTrue(spy.Lines.Contains("LOGGING >"));
            Assert.IsTrue(spy.Lines.Any(l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal)));
        }
    }
}

