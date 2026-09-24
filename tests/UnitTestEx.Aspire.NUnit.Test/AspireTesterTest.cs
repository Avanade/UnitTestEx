using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using UnitTestEx.Abstractions;
using UnitTestEx.Api.Models;

namespace UnitTestEx.Aspire.NUnit.Test
{
    [TestFixture]
    public class AspireTesterTest
    {
        [Test]
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

        [Test]
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

        [Test]
        public async Task Reason_And_Delay_AggregatesResourceLogs()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var spy = new SpyTestFrameworkImplementor(tester.Implementor);
            tester.ReplaceTestFrameworkImplementor(spy);

            var reasonResult = tester.Reason("Confirming Reason() writes context for Aspire multi-host testers too.");

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

            Assert.That(reasonResult, Is.SameAs(tester));
            Assert.That(delayResult, Is.SameAs(tester));
            Assert.That(sw.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200)), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.That(spy.Lines, Does.Contain("REASON >"));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("DELAY (00:00:02) >")), Is.True);
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("Waiting for a background Person lookup to complete and log.")), Is.True);
            Assert.That(spy.Lines, Does.Contain("LOGGING >"));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal)), Is.True);
        }
    }
}

