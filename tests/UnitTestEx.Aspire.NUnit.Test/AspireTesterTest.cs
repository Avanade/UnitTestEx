using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
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
        public async Task Reason_And_WaitAndLog_AggregatesResourceLogs()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var spy = new SpyTestFrameworkImplementor(tester.Implementor);
            tester.ReplaceTestFrameworkImplementor(spy);

            var reasonResult = tester.Reason("Confirming Reason() writes context for Aspire multi-host testers too.");

            // Fire a request against the 'api' resource part-way through the wait window to simulate background/inter-resource activity.
            var backgroundCallTask = Task.Run(async () =>
            {
                await Task.Delay(300);
                tester.Http("api").Run(HttpMethod.Get, "Person/1");
            });

            var sw = Stopwatch.StartNew();
            var waitResult = tester.WaitAndLog("Waiting for a background Person lookup to complete and log.", TimeSpan.FromSeconds(2));
            sw.Stop();

            await backgroundCallTask;

            Assert.That(reasonResult, Is.SameAs(tester));
            Assert.That(waitResult, Is.SameAs(tester));
            Assert.That(sw.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200)), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.That(spy.Lines, Does.Contain("REASON >"));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("WAIT >") && l.Contains("Waiting for a background Person lookup to complete and log.")), Is.True);
            Assert.That(spy.Lines, Does.Contain("LOGGING >"));
            Assert.That(spy.Lines.Any(l => l != null && l.Contains("[api]") && l.Contains("Get using identifier 1.")), Is.True);
        }
    }
}

