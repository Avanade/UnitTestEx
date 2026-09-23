using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using UnitTestEx;
using UnitTestEx.Abstractions;
using UnitTestEx.Api.Models;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Aspire.Xunit.Test
{
    public class AspireTesterTest : UnitTestBase
    {
        public AspireTesterTest(ITestOutputHelper output) : base(output) { }

        [Fact]
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

        [Fact]
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

        [Fact]
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

            Assert.Same(tester, reasonResult);
            Assert.Same(tester, waitResult);
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200), $"Expected to wait ~2s, actually waited {sw.Elapsed}.");
            Assert.Contains("REASON >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("WAIT (00:00:02) >"));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Waiting for a background Person lookup to complete and log."));
            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal));
        }

        [Fact]
        public async Task HttpMock_StubsExternalGatewayDependency_Product()
        {
            // Note: this exercises a real WireMock.Net container resource (added via 'gateway' in the AppHost using the official WireMock.Net.Aspire package); it requires Docker/Podman to
            // be available to the CI/dev machine running the test (see docs/design/aspire-multi-host-testing.md).
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("gateway");

            var stub = await tester.HttpMock("gateway")
                .Request(HttpMethod.Get, "/products/abc")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

            tester.Http("api")
                .Run(HttpMethod.Get, "Product/abc")
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task HttpMock_WithSequenceAsync_ReturnsResponsesInOrderThenExhausts()
        {
            // Note: this exercises WireMock.Net's Scenario/state mechanism (see docs/design/aspire-multi-host-testing.md); each subsequent invocation returns the next configured
            // response. Unlike a single stub, a sequence's own completeness (exactly one invocation per configured response) IS the expectation - mirroring Tier 1's WithSequence -
            // so Times cannot be combined with it, and any invocation beyond the configured responses receives a distinct 500 "exhausted" response instead of silently repeating.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("gateway");

            var stub = await tester.HttpMock("gateway")
                .Request(HttpMethod.Get, "/products/seq")
                .WithAnyBody()
                .Respond.WithSequenceAsync(seq =>
                {
                    seq.Respond().WithJson(new { id = "Seq", description = "First" });
                    seq.Respond().WithJson(new { id = "Seq", description = "Second" });
                    seq.Respond().WithJson(new { id = "Seq", description = "Third" });
                });

            tester.Http("api").Run(HttpMethod.Get, "Product/seq").AssertOK().AssertValue(new { id = "Seq", description = "First" });
            tester.Http("api").Run(HttpMethod.Get, "Product/seq").AssertOK().AssertValue(new { id = "Seq", description = "Second" });
            tester.Http("api").Run(HttpMethod.Get, "Product/seq").AssertOK().AssertValue(new { id = "Seq", description = "Third" });

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task HttpMock_WithSequenceAsync_ExceedingConfiguredResponses_Throws()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("gateway");

            var stub = await tester.HttpMock("gateway")
                .Request(HttpMethod.Get, "/products/seq")
                .WithAnyBody()
                .Respond.WithSequenceAsync(seq => seq.Respond().WithJson(new { id = "Seq", description = "Only" }));

            tester.Http("api").Run(HttpMethod.Get, "Product/seq").AssertOK().AssertValue(new { id = "Seq", description = "Only" });
            tester.Http("api").Run(HttpMethod.Get, "Product/seq").AssertInternalServerError();

            await Assert.ThrowsAsync<MockHttpClientException>(() => stub.VerifyAsync());
        }

        [Fact]
        public async Task HttpMock_WithJsonBody_PathsToIgnore_IgnoresSpecifiedProperty()
        {
            // Note: exercises WireMock.Net's own JsonPartialMatcher (see docs/design/aspire-multi-host-testing.md) - a variable 'eTag' is ignored from the match pattern, while a
            // genuinely differing (non-ignored) 'id' still fails to match any stub (WireMock.Net's default "no mapping found" 404 response).
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("gateway");

            var stub = await tester.HttpMock("gateway")
                .Request(HttpMethod.Post, "/echo")
                .Times(Times.Once())
                .WithJsonBody(new { id = "Abc", eTag = "pattern-etag-value" }, "eTag")
                .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

            tester.Http("gateway").Run(HttpMethod.Post, "/echo", new { id = "Abc", eTag = Guid.NewGuid().ToString() })
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });

            tester.Http("gateway").Run(HttpMethod.Post, "/echo", new { id = "Xyz", eTag = "whatever" }).AssertNotFound();

            await stub.VerifyAsync();
        }
    }
}

