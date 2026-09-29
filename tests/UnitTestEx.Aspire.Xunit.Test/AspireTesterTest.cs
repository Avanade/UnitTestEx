using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Moq;
using UnitTestEx;
using UnitTestEx.Abstractions;
using UnitTestEx.Api.Models;
using UnitTestEx.Json;
using UnitTestEx.Mocking;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

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
        public async Task BeforeStart_InvokedInOrder_BeforeAnyResourceStarts()
        {
            var order = new System.Collections.Generic.List<string>();

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(app =>
                {
                    Assert.NotNull(app);
                    order.Add("first");
                    return Task.CompletedTask;
                })
                .BeforeStart(app =>
                {
                    Assert.NotNull(app);
                    order.Add("second");
                    return Task.CompletedTask;
                });

            // Triggers build+start; both BeforeStart callbacks must already have run (in registration order) before this returns.
            await tester.WaitForResourceAsync("api");

            Assert.Equal(["first", "second"], order);
        }

        [Fact]
        public async Task BeforeStart_Throws_AbortsStartUpAndPropagates()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(_ => throw new InvalidOperationException("Simulated pre-start failure."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tester.WaitForResourceAsync("api"));
            Assert.Equal("Simulated pre-start failure.", ex.Message);
        }

        [Fact]
        public async Task BeforeStart_CanResolveConnectionStringResource()
        {
            string? connectionString = null;

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(async app => connectionString = await AspireTesterBase.GetConnectionStringAsync(app, "test-db"));

            // Triggers build+start; GetConnectionStringAsync must have already resolved the value above - Aspire's own testing extension
            // of the same name would throw here as the application has not yet started (see AspireTesterBase.GetConnectionStringAsync's remarks).
            await tester.WaitForResourceAsync("api");

            Assert.Equal("Data Source=unit-test;", connectionString);
        }

        [Fact]
        public async Task AfterStart_InvokedInOrder_AfterBeforeStartAndResourcesStarted()
        {
            var order = new System.Collections.Generic.List<string>();

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .BeforeStart(app =>
                {
                    Assert.NotNull(app);
                    order.Add("before");
                    return Task.CompletedTask;
                })
                .AfterStart(app =>
                {
                    Assert.NotNull(app);
                    order.Add("first");
                    return Task.CompletedTask;
                })
                .AfterStart(app =>
                {
                    Assert.NotNull(app);
                    order.Add("second");
                    return Task.CompletedTask;
                });

            // Triggers build+start; BeforeStart, then both AfterStart callbacks (in registration order), must already have run before this returns.
            await tester.WaitForResourceAsync("api");

            Assert.Equal(["before", "first", "second"], order);
        }

        [Fact]
        public async Task AfterStart_Throws_AbortsStartUpAndPropagates()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .AfterStart(_ => throw new InvalidOperationException("Simulated post-start failure."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tester.WaitForResourceAsync("api"));
            Assert.Equal("Simulated post-start failure.", ex.Message);
        }

        [Fact]
        public async Task AfterStart_CanWaitForResourceHealthy_UsingExtensionMethod()
        {
            var waited = false;

            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .AfterStart(async app =>
                {
                    // Safe to call from within AfterStart as it operates directly against 'app', unlike the instance WaitForResourceAsync which would deadlock here.
                    await app.WaitForResourceAsync("api");
                    waited = true;
                });

            await tester.WaitForResourceAsync("api");

            Assert.True(waited);
        }

        [Fact]
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

            Assert.Same(tester, checkpointResult);
            Assert.Same(tester, delayResult);
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.Contains("CHECKPOINT >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("DELAY (00:00:02) >"));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Waiting for a background Person lookup to complete and log."));
            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ErrorWhenLogContains_DefaultsToEnabled_ThrowsWithoutExplicitRegistration()
        {
            // ErrorWhenLogContains was never explicitly called - it is enabled by default (at LogLevel.Error), being a brand-new capability, so an Error-level resource log must still fail.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var ex = Assert.Throws<XunitException>(() => tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error"));
            Assert.Contains("Simulated error log entry.", ex.Message);
        }

        [Fact]
        public async Task ErrorWhenLogContains_None_OptsOutEntirely()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None); // LogLevel.None is numerically above Critical, so nothing can ever meet/exceed it - the documented opt-out.

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
        }

        [Fact]
        public async Task ErrorWhenLogContains_Http_ThrowsWhenResourceLogsAtOrAboveDefaultMinimumLevel()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(); // Defaults to LogLevel.Error.

            await tester.WaitForResourceAsync("api");

            var ex = Assert.Throws<XunitException>(() => tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error"));
            Assert.Contains("Simulated error log entry.", ex.Message);
        }

        [Fact]
        public async Task ErrorWhenLogContains_BelowConfiguredMinimumLevel_DoesNotThrow()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.Critical); // An 'error' level log does not meet this higher 'critical' minimum.

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
        }

        [Fact]
        public async Task ErrorWhenLogContains_ExcludeWildcard_SuppressesOtherwiseViolatingEntry()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.Error, exclude: ["Simulated error*"]);

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
        }

        [Fact]
        public async Task ErrorWhenLogContains_IncludeWildcard_DoesNotThrowWhenEntryDoesNotMatch()
        {
            // 'include' narrows what is checked - an otherwise-qualifying (Error level) entry that matches none of the include patterns is not reported.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(include: ["*a totally different subsystem*"]);

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
        }

        [Fact]
        public async Task ErrorWhenLogContains_IncludeWildcard_ThrowsWhenEntryMatches()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(include: ["Simulated error*"]);

            await tester.WaitForResourceAsync("api");

            var ex = Assert.Throws<XunitException>(() => tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error"));
            Assert.Contains("Simulated error log entry.", ex.Message);
        }

        [Fact]
        public async Task ErrorWhenLogContains_ExcludeWinsOverInclude_SuppressesEvenWhenIncludeMatches()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(exclude: ["Simulated error*"], include: ["Simulated*"]);

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
        }

        [Fact]
        public async Task ErrorWhenLogContains_RepeatCall_ReplacesRatherThanMergesPriorConfiguration()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(exclude: ["Simulated error*"]) // Would otherwise suppress the entry below...
                .ErrorWhenLogContains(); // ...but this second call replaces the entire prior configuration - the exclude pattern above is now gone.

            await tester.WaitForResourceAsync("api");

            var ex = Assert.Throws<XunitException>(() => tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error"));
            Assert.Contains("Simulated error log entry.", ex.Message);
        }

        [Fact]
        public async Task ErrorWhenLogContains_ChecksOnFinalCheckpoint_ForActivityNotDrainedByHttp()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains();

            await tester.WaitForResourceAsync("api");

            // Fire a raw (uninstrumented) request so the resulting Error log line is captured but not claimed/checked by any tester-driven Http() call - only a subsequent
            // Checkpoint/Delay call (here, the final Checkpoint recommended at the end of a test relying on ErrorWhenLogContains) drains and checks it.
            using (var client = ((IHttpClientSource)tester).CreateHttpClient("api"))
            {
                using var response = await client.GetAsync("Person/test/log/error");
                response.EnsureSuccessStatusCode();
            }

            await Task.Delay(300); // Allow the forwarded resource log line time to arrive/be captured before draining.

            Assert.Throws<XunitException>(() => tester.Checkpoint("Final log check."));
        }

        [Fact]
        public async Task AssertLogContains_FindsExpectedText_AcrossAllOrASpecificResource()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None); // Deliberately induces an Error-level resource log as the very thing under test.

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();

            tester.AssertLogContains("Simulated error log*"); // Across every resource.
            tester.AssertLogContains("Simulated error log*", "api"); // Scoped to just the 'api' resource.
            tester.AssertLogNotContains("This text was never logged.");
        }

        [Fact]
        public async Task AssertLogContains_ThrowsWhenTextNotFound()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");

            var ex = Assert.Throws<XunitException>(() => tester.AssertLogContains("This text was never logged."));
            Assert.Contains("Expected a resource log entry to contain", ex.Message);
        }

        [Fact]
        public async Task AssertLogNotContains_ThrowsWhenTextFound()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None);

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();

            var ex = Assert.Throws<XunitException>(() => tester.AssertLogNotContains("Simulated error*"));
            Assert.Contains("Simulated error log entry.", ex.Message);
        }

        [Fact]
        public async Task AssertLogContains_FindsUndrainedActivity_WithoutRequiringCheckpoint()
        {
            // Unlike ErrorWhenLogContains (only checked as entries are subsequently drained via Checkpoint/Delay/Http), AssertLogContains checks every resource log entry captured so
            // far regardless of whether it has already been drained - so, unlike ErrorWhenLogContains_ChecksOnFinalCheckpoint_ForActivityNotDrainedByHttp above, no trailing Checkpoint
            // is needed to "see" a raw, uninstrumented request's resulting log line.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None);

            await tester.WaitForResourceAsync("api");

            using (var client = ((IHttpClientSource)tester).CreateHttpClient("api"))
            {
                using var response = await client.GetAsync("Person/test/log/error");
                response.EnsureSuccessStatusCode();
            }

            await Task.Delay(300); // Allow the forwarded resource log line time to arrive/be captured.

            tester.AssertLogContains("Simulated error log entry.");
        }

        [Fact]
        public async Task ResetLogs_DiscardsPreviouslyCapturedEntries()
        {
            // Simulates a shared-host scenario: log activity from "before" the reset (e.g. a prior test sharing the same host) must not be visible to any check performed "after" it.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None);

            await tester.WaitForResourceAsync("api");

            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
            tester.AssertLogContains("Simulated error log entry.");

            tester.ResetLogs();

            var ex = Assert.Throws<XunitException>(() => tester.AssertLogContains("Simulated error log entry."));
            Assert.Contains("Expected a resource log entry to contain", ex.Message);

            // Fresh activity logged after the reset must still be captured/found as normal.
            tester.Http("api").Run(HttpMethod.Get, "Person/test/log/error").AssertOK();
            tester.AssertLogContains("Simulated error log entry.");
        }

        [Fact]
        public async Task HttpMock_StubsExternalMockHostDependency_Product()
        {
            // Note: this exercises the self-hosted WireMock.Net project resource (added via 'mockhost' in the AppHost - see AppHost.cs's header comment); no Docker/Podman is required.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var stub = await tester.HttpMock("mockhost")
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
        public async Task HttpMock_StubsExternalMockHostDependency_RequestUriWithoutLeadingSlash_IsNormalized()
        {
            // WireMock.Net's admin API rejects a mapping path lacking a leading '/'; AspireHttpMockRequest must add one automatically (consistent with Tier 1's MockHttpClientRequest,
            // which does not require one either) so callers do not need to remember to prefix every requestUri themselves.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var stub = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Get, "products/abc") // Deliberately no leading '/'.
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
        public async Task HttpMock_StubbedTraffic_LogsRequestAndResponse()
        {
            // WireMockRequestResponseLogger gives Tier 2/3 parity with Tier 1's MockHttpClientHandler LogDebug request/response logging - here at its default (Information) LogLevel, since
            // this resource logging drains through the same captured resource-log mechanism used by Checkpoint/Delay/ErrorWhenLogContains.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var spy = new SpyTestFrameworkImplementor(tester.Implementor);
            tester.ReplaceTestFrameworkImplementor(spy);

            var stub = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Get, "/products/abc")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

            tester.Http("api")
                .Run(HttpMethod.Get, "Product/abc")
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });

            await stub.VerifyAsync();

            tester.Checkpoint("Final log check.");

            Assert.Contains(spy.Lines, l => l != null && l.Contains("UnitTestEx > Sending HTTP request GET /products/abc") && l.Contains("(mockhost)]"));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("UnitTestEx > Received HTTP response 200") && l.Contains("(mockhost)]"));
        }

        [Fact]
        public async Task HttpMock_ViaDistributedApplicationExtensionMethod_WorksWithoutTester()
        {
            // Proves the standalone 'DistributedApplication.HttpMock' extension method (see UnitTestExAspireExtensions) configures the same underlying WireMock.Net resource
            // as AspireTesterBase.HttpMock above, without needing a tester at all - e.g. usable directly from an AppHost.cs itself (after 'await app.StartAsync()') to pre-seed
            // default stubs so an exploratory/manual run doesn't fail against an un-stubbed external dependency; AfterStart is used here purely to get at the raw 'app'.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .AfterStart(async app =>
                {
                    await app.WaitForResourceAsync("mockhost");

                    _ = await app.HttpMock("mockhost")
                        .Request(HttpMethod.Get, "/products/xyz")
                        .Times(Times.Once())
                        .WithAnyBody()
                        .Respond.WithJsonAsync(new { id = "Xyz", description = "Configured via the DistributedApplication.HttpMock extension method." });
                });

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            tester.Http("mockhost").Run(HttpMethod.Get, "/products/xyz")
                .AssertOK()
                .AssertValue(new { id = "Xyz", description = "Configured via the DistributedApplication.HttpMock extension method." });
        }

        [Fact]
        public async Task HttpMock_WithSequenceAsync_ReturnsResponsesInOrderThenExhausts()
        {
            // Note: this exercises WireMock.Net's Scenario/state mechanism; each subsequent invocation returns the next configured
            // response. Unlike a single stub, a sequence's own completeness (exactly one invocation per configured response) IS the expectation - mirroring Tier 1's WithSequence -
            // so Times cannot be combined with it, and any invocation beyond the configured responses receives a distinct 500 "exhausted" response instead of silently repeating.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var stub = await tester.HttpMock("mockhost")
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
            // Deliberately induces a genuine 500/Error-level resource log as the very thing under test - opts out of the (now default-on) ErrorWhenLogContains check entirely rather than
            // having it collide with the intentionally-triggered failure below.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .ErrorWhenLogContains(LogLevel.None);

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var stub = await tester.HttpMock("mockhost")
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
            // Note: exercises WireMock.Net's own JsonPartialMatcher - a variable 'eTag' is ignored from the match pattern, while a
            // genuinely differing (non-ignored) 'id' still fails to match any stub (WireMock.Net's default "no mapping found" 404 response).
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            var stub = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Post, "/echo")
                .Times(Times.Once())
                .WithJsonBody(new { id = "Abc", eTag = "pattern-etag-value" }, "eTag")
                .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Abc", eTag = Guid.NewGuid().ToString() })
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "A blue carrot" });

            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Xyz", eTag = "whatever" }).AssertNotFound();

            await stub.VerifyAsync();
        }
        [Fact]
        public async Task HttpMock_SharedInterface_ConfiguresIdenticallyAcrossTiers()
        {
            // Proves that the exact same test-authoring code (HttpMockSharedConfig.ConfigureProductStubAsync, written once against IHttpMockClient) can configure Tier 2/3's
            // AspireHttpMockClient identically to Tier 1's MockHttpClient (see the equivalent test in UnitTestEx.Xunit.Test).
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync(["api", "mockhost"]);

            var stub = await HttpMockSharedConfig.ConfigureProductStubAsync(tester.HttpMock("mockhost"), "/products/shared");

            tester.Http("api")
                .Run(HttpMethod.Get, "Product/shared")
                .AssertOK()
                .AssertValue(new { id = "Shared", description = "Configured via the shared IHttpMockClient interface." });

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task HttpMock_WithRequestsFromResourceAsync_LoadsStubsFromYaml_IdenticallyToTier1()
        {
            // Proves the shared IHttpMockClient.WithRequestsFromResourceAsync DIM (see HttpMockResourceConfig in the core UnitTestEx project) loads the exact same YAML/JSON schema
            // as Tier 1's native MockHttpClient.WithRequestsFromResource against a real WireMock.Net mockhost resource - closing the feature gap between the two tiers.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            IHttpMockClient client = tester.HttpMock("mockhost");
            await client.WithRequestsFromResourceAsync<AspireTesterTest>("AspireTesterTest-mock.unittestex.yaml");

            tester.Http("mockhost").Run(HttpMethod.Get, "/resource-config/simple")
                .AssertOK()
                .AssertValue(new { id = "FromResource", description = "Loaded from shared YAML resource." });

            tester.Http("mockhost").Run(HttpMethod.Post, "/resource-config/echo", new { id = "Abc", stamp = Guid.NewGuid().ToString() })
                .AssertAccepted()
                .AssertValue(new { id = "Abc", description = "matched" });
        }

        [Fact]
        public async Task HttpMock_InterfaceCoreMembers_TierSpecificAdaptations()
        {
            // Unlike HttpMockInterfaceTest.cs in UnitTestEx.Xunit.Test (which exercises the shared IHttpMock* DIM "extras" - identical bytecode on both tiers), this exercises
            // AspireHttpMockRequest/AspireHttpMockResponse's own hand-written, tier-specific explicit interface implementations of the *core* (non-DIM) members via interface-typed
            // variables: the 2-arg WithBody, WithJsonBody<T>, both branches (with/without content) of the WithAsync adaptation, and the Action<IHttpMockResponseSequence>-wrapping
            // adapter within WithSequenceAsync.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            IHttpMockClient client = tester.HttpMock("mockhost");

            var stub1 = await client.Request(HttpMethod.Post, "/interface/with-body")
                .Times(Times.Once())
                .WithBody("plain-text-body", MediaTypeNames.Text.Plain)
                .Respond.WithAsync("received", HttpStatusCode.OK, MediaTypeNames.Text.Plain);

            var stub2 = await client.Request(HttpMethod.Get, "/interface/no-content")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithAsync(statusCode: HttpStatusCode.NoContent);

            var stub3 = await client.Request(HttpMethod.Post, "/interface/json-body")
                .Times(Times.Once())
                .WithJsonBody(new { id = "Abc" })
                .Respond.WithJsonAsync(new { id = "Abc", description = "Configured via IHttpMockRequest.WithJsonBody<T>." });

            var stub4 = await client.Request(HttpMethod.Get, "/interface/sequence")
                .WithAnyBody()
                .Respond.WithSequenceAsync(seq =>
                {
                    seq.Respond().WithJson(new { id = "First" });
                    seq.Respond().WithJson(new { id = "Second" });
                });

            tester.Http("mockhost").Run(HttpMethod.Post, "/interface/with-body", "plain-text-body", MediaTypeNames.Text.Plain)
                .AssertOK()
                .AssertContent("received");

            tester.Http("mockhost").Run(HttpMethod.Get, "/interface/no-content").AssertNoContent();

            tester.Http("mockhost").Run(HttpMethod.Post, "/interface/json-body", new { id = "Abc" })
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "Configured via IHttpMockRequest.WithJsonBody<T>." });

            tester.Http("mockhost").Run(HttpMethod.Get, "/interface/sequence").AssertOK().AssertValue(new { id = "First" });
            tester.Http("mockhost").Run(HttpMethod.Get, "/interface/sequence").AssertOK().AssertValue(new { id = "Second" });

            await stub1.VerifyAsync();
            await stub2.VerifyAsync();
            await stub3.VerifyAsync();
            await stub4.VerifyAsync();
        }

        [Fact]
        public async Task HttpMock_WithJsonBodyUsingUnitTestExComparer_SelfHostedMockHost_MatchesUsingJsonElementComparerSemantics()
        {
            // 'mockhost' is UnitTestEx.Aspire.MockHost - the recommended, self-hosted WireMock.Net project resource (a sample a consumer would copy into their own solution; no
            // Docker/Podman needed) that registers UnitTestEx.Aspire's JsonElementComparerMatcher, giving genuine JsonElementComparer semantics rather than WireMock.Net's own JSON comparison.
            //
            // The differentiator this proves: JsonElementComparer performs semantic value coercion for dates (and GUIDs/numbers) - "2024-01-01T00:00:00Z" and
            // "2024-01-01T00:00:00.000+00:00" are considered equal even though they differ textually. WireMock.Net's own JsonMatcher (see WithJsonBody, which 'mockhost' also
            // supports, being a genuine WireMock.Net server) is a textual/structural comparison and would NOT consider these equal - only WithJsonBodyUsingUnitTestExComparer's
            // custom matcher can bridge that gap. (Where strict, WireMock-style textual matching is instead wanted, configure JsonElementComparerOptions.Exact rather than
            // falling back to WithJsonBody.)
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            var stub = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Post, "/echo")
                .Times(Times.Once())
                .WithJsonBodyUsingUnitTestExComparer(new { id = "Abc", occurredAt = "2024-01-01T00:00:00Z" })
                .Respond.WithJsonAsync(new { id = "Abc", description = "Matched via JsonElementComparer semantics." });

            // Same instant, different (but equally valid) textual representation - JsonElementComparer's semantic date coercion still considers this a match.
            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Abc", occurredAt = "2024-01-01T00:00:00.000+00:00" })
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "Matched via JsonElementComparer semantics." });

            await stub.VerifyAsync();

            // A genuinely different (non-coercible) value still fails to match any stub (WireMock.Net's default "no mapping found" 404 response).
            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Xyz", occurredAt = "2024-01-01T00:00:00Z" }).AssertNotFound();
        }

        [Fact]
        public async Task HttpMock_WithJsonBodyUsingUnitTestExComparer_HonoursLiveJsonComparerOptions()
        {
            // Proves that WithJsonBodyUsingUnitTestExComparer captures *this tester's* current JsonComparerOptions (set via UseJsonComparerOptions) into the matcher's payload -
            // not JsonElementComparer.Default - since the matcher runs in a separate OS process (the self-hosted 'mockhost') with no access to this test process's static state.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            // Switch this tester to strict (WireMock-style) textual value comparison before configuring the mapping.
            tester.UseJsonComparerOptions(new JsonElementComparerOptions { ValueComparison = JsonElementComparison.Exact });

            var stub = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Post, "/echo")
                .Times(Times.Once())
                .WithJsonBodyUsingUnitTestExComparer(new { id = "Abc", occurredAt = "2024-01-01T00:00:00Z" })
                .Respond.WithJsonAsync(new { id = "Abc", description = "Matched via JsonElementComparer semantics." });

            // Same instant, different textual representation - under Exact this is no longer considered a match (unlike the Semantic-mode test above).
            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Abc", occurredAt = "2024-01-01T00:00:00.000+00:00" }).AssertNotFound();

            // The exact same textual representation still matches.
            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", new { id = "Abc", occurredAt = "2024-01-01T00:00:00Z" })
                .AssertOK()
                .AssertValue(new { id = "Abc", description = "Matched via JsonElementComparer semantics." });

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task HttpMock_WithJsonBodyUsingUnitTestExComparer_InvalidJsonRequestBodyStillTreatedAsCleanNonMatch()
        {
            // Proves that a request body that is not valid JSON is treated as an ordinary non-match (404, "no matching mapping found") rather than faulting the mock host's
            // request pipeline. Internally this attaches a JsonElementComparerMatcherException to the WireMock.Net MatchResult - the one case where the matcher does so, since
            // it's a genuine evaluation failure (consistent with WireMock.Net's own built-in matchers); this is deliberately NOT done for an ordinary semantic mismatch, as
            // WireMock.Net's MappingMatcher.FindBestMatch treats ANY non-null MatchResult.Exception as "this mapping failed to evaluate" and excludes it from partial/closest-match
            // tracking entirely (see JsonElementComparerMatcher.IsMatch remarks) - so attaching one for every mismatch would silently break WireMock.Net's own "closest match"
            // debugging rather than improve it.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            await tester.WaitForResourceAsync("mockhost");

            _ = await tester.HttpMock("mockhost")
                .Request(HttpMethod.Post, "/echo")
                .WithJsonBodyUsingUnitTestExComparer(new { id = "Abc" })
                .Respond.WithJsonAsync(new { id = "Abc" });

            tester.Http("mockhost").Run(HttpMethod.Post, "/echo", "{ not valid json", MediaTypeNames.Application.Json).AssertNotFound();
        }

        [Fact]
        public async Task UseSetUp_And_UseUser_FlowThroughToHttpRequestSend()
        {
            string? capturedUserName = null;
            HttpRequestMessage? capturedRequest = null;

            var setUp = new TestSetUp
            {
                OnBeforeHttpRequestMessageSendAsync = (request, userName, _) =>
                {
                    capturedUserName = userName;
                    capturedRequest = request;
                    request.Headers.Add("X-Test-User", userName);
                    return Task.CompletedTask;
                }
            };

            // UseSetUp/UseUser mirror TesterBase<TSelf>'s equivalents; unlike Tier 1 there is no host to reset - SetUp is consumed live by HttpTester at send time.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue")
                .UseSetUp(setUp)
                .UseUser("Jane");

            await tester.WaitForResourceAsync("api");

            tester.Http("api")
                .Run(HttpMethod.Get, "Person?firstName=John&lastName=Doe")
                .AssertOK();

            Assert.Equal("Jane", capturedUserName);
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.Headers.Contains("X-Test-User"));
        }

        [Fact]
        public async Task UseJsonSerializer_And_UseJsonComparerOptions_UpdatesTesterProperties()
        {
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();

            var serializer = new JsonSerializer();
            var options = new JsonElementComparerOptions { NullComparison = JsonElementComparison.Exact };

            tester.UseJsonSerializer(serializer).UseJsonComparerOptions(options);

            Assert.Same(serializer, tester.JsonSerializer);
            Assert.Same(options, tester.JsonComparerOptions);
        }
    }
}

