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

            Assert.Same(tester, reasonResult);
            Assert.Same(tester, delayResult);
            Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(200), $"Expected to delay ~2s, actually waited {sw.Elapsed}.");
            Assert.Contains("REASON >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("DELAY (00:00:02) >"));
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Waiting for a background Person lookup to complete and log."));
            Assert.Contains("LOGGING >", spy.Lines);
            Assert.Contains(spy.Lines, l => l != null && l.Contains("Get using identifier 1.") && l.EndsWith("(api)]", StringComparison.Ordinal));
        }

        [Fact]
        public async Task HttpMock_StubsExternalMockHostDependency_Product()
        {
            // Note: this exercises the self-hosted WireMock.Net project resource (added via 'mockhost' in the AppHost - see AppHost.cs's header comment); no Docker/Podman is required.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("mockhost");

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
        public async Task HttpMock_WithSequenceAsync_ReturnsResponsesInOrderThenExhausts()
        {
            // Note: this exercises WireMock.Net's Scenario/state mechanism; each subsequent invocation returns the next configured
            // response. Unlike a single stub, a sequence's own completeness (exactly one invocation per configured response) IS the expectation - mirroring Tier 1's WithSequence -
            // so Times cannot be combined with it, and any invocation beyond the configured responses receives a distinct 500 "exhausted" response instead of silently repeating.
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("mockhost");

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
            await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
                .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("mockhost");

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

            await tester.WaitForResourceAsync("api");
            await tester.WaitForResourceAsync("mockhost");

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

