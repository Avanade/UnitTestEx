# Aspire multi-host testing

[← Back to README](../README.md)


The single-host testers (`ApiTester`, `FunctionTester`, `GenericTester`, etc.) hosts a **single** system/service in-process via the likes of `WebApplicationFactory` - ideal for intra-domain testing where you want deep, per-component control (DI replacement, mocked `HttpClient`s) of one service in isolation.

Sometimes, though, you genuinely need to prove that two or more **real**, separately-hosted services interact correctly over the network (inter-domain testing) - for example a [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) distributed application where a "shopping" API calls a "products" API. For this, the [`UnitTestEx.Aspire`](../src/UnitTestEx.Aspire) package provides `AspireTester`, which spins up the **entire AppHost** - every project, container and executable resource it declares - as separate, real OS processes wired together with Aspire's actual service discovery, exactly as they'd run in production. This is deliberately a second, opt-in tier rather than an extension of the first: pick the tier per test based on what you're actually trying to prove, don't force a hybrid.

This isn't a UnitTestEx-specific compromise - it matches Aspire's own guidance verbatim:

> "If your goal is to test a single project in isolation, run components in-memory, or mock external dependencies, consider using `WebApplicationFactory<T>` instead."
> — [aspire.dev/testing/overview](https://aspire.dev/testing/overview/)

``` csharp
await using var tester = AspireTester.Create<Projects.MyAppHost>();

await tester.WaitForResourceAsync("shopping");

tester.Http("shopping")
    .Run(HttpMethod.Get, "orders/123")
    .AssertOK()
    .AssertValue(new { id = "123", product = "Widget" });
```

For an NUnit fixture that deliberately shares one AppHost across its test methods, derive from `WithAspireTester<TAppHost>` and use its `Test` property. The base exposes `OnBeforeStartAsync` and `OnAfterStartAsync` hooks and asynchronously disposes the shared tester when the fixture is torn down:

```csharp
public sealed class ShoppingTests : WithAspireTester<Projects.MyAppHost>
{
    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Test.WithResourceEnvironment("shopping", "FeatureFlag", "enabled");
        await Test.WaitForResourceAsync("shopping");
    }

    [Test]
    public void CanReadAnOrder()
        => Test.Http("shopping").Run(HttpMethod.Get, "orders/123").AssertOK();
}
```

This is a lifecycle convenience, not automatic test isolation: resources, external data, and captured logs live across methods for as long as the shared AppHost is running. Reset mutable external state and call `Test.ResetLogs()` where a test depends on a clean starting point. Use a fresh `AspireTester.Create<TAppHost>()` per test when isolation matters more than AppHost startup time. The base implements `IAsyncDisposable`; dispose it from the test framework's asynchronous fixture teardown.

External-to-the-solution dependencies (an email/notification provider, an identity/auth service, an ERP system, a payment gateway, etc.) still need to be mocked - a real inter-domain test proves *your* services talk to each other correctly, not that a third-party's sandbox environment is up. UnitTestEx's recommended pattern is to **self-host** [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) as an ordinary Aspire *project* resource (`AddProject`) - one small console app per external system, added to your own solution - rather than the official [`WireMock.Net.Aspire`](https://github.com/WireMock-Net/WireMock.Net-Aspire) package's *container* resource (`AddWireMock`). UnitTestEx does not ship this host as a package (there's nothing to install or version); [`UnitTestEx.Aspire.MockHost`](../tests/UnitTestEx.Aspire.MockHost) is a template you copy into your own solution (referencing `UnitTestEx.Aspire`, which ships both the `JsonElementComparerMatcher` type and the `WireMockConsole.RunAsync` helper used below, so there's no matcher code - or port/custom-matcher/graceful-shutdown boilerplate - to write yourself):

``` csharp
// MockApis/Program.cs
await WireMockConsole.RunAsync(settings => WireMockServer.Start(settings));
```

`WireMockConsole.RunAsync` reads the `PORT` environment variable Aspire assigns, builds an `AspireWireMockServerSettings` (a plain `WireMockServerSettings` subclass - `WireMock.Net` deliberately leaves it unsealed - that adds UnitTestEx-specific configuration as a single, discoverable extension point rather than an ever-growing list of `RunAsync` parameters) with `JsonElementComparerMatcher` already registered, invokes your factory to start the actual server (any `IWireMockServer` - typically `WireMockServer.Start`), then blocks gracefully until Aspire stops the process (Ctrl+C locally, SIGTERM in orchestration), disposing the server on the way out. It also registers a `WireMockRequestResponseLogger` (giving Tier 2/3 parity with Tier 1's `MockHttpClientHandler` request/response logging above) that logs every genuine stubbed request/response pair - not the admin API calls used to configure mappings - through the resource's own console output at `AspireWireMockServerSettings.RequestResponseLogLevel` (`LogLevel.Information` by default), so it is captured and drained the same way as any other resource log entry (see `Checkpoint`/`Delay`/`ErrorWhenLogContains` below). Set a different level (or `LogLevel.None` to disable) directly on the settings your factory receives:

``` csharp
await WireMockConsole.RunAsync(settings =>
{
    settings.RequestResponseLogLevel = LogLevel.Debug;
    return WireMockServer.Start(settings);
});
```

``` csharp
// AppHost.cs
var email = builder.AddMockHostProject<Projects.MockApis>("email");
var auth  = builder.AddMockHostProject<Projects.MockApis>("auth");
var erp   = builder.AddMockHostProject<Projects.MockApis>("erp");

builder.AddProject<Projects.MyApi>("api")
    .WithMockHostEnvironment("Email__BaseUrl", email, "http")
    .WithMockHostEnvironment("Auth__BaseUrl", auth, "http")
    .WithMockHostEnvironment("Erp__BaseUrl", erp, "http");
```

`AddMockHostProject`/`WithMockHostEnvironment` (both extension methods on `UnitTestEx.Aspire`, in the `Aspire.Hosting` namespace alongside Aspire's own `AddProject`/`WithEnvironment`) exist because each mock resource is test-only and must never appear in a *published manifest* - the JSON resource graph `aspire publish` (or `dotnet run --publisher manifest`) emits for deployment tooling (e.g. Azure Developer CLI) to turn into real infrastructure; a mock host has no production equivalent, so including it there would make deployment tooling try to provision it as if it were real. `AddMockHostProject` only actually adds the resource when `IDistributedApplicationBuilder.ExecutionContext.IsRunMode` is `true` (returning `null` in publish mode instead), and `WithMockHostEnvironment` accepts that potentially-`null` result directly, no-op'ing rather than requiring an `if (email is not null)` guard at every call site.

Being just our own process (not a published, off-the-shelf binary), it can register `JsonElementComparerMatcher` - a custom `IMatcher`, shipped as part of `UnitTestEx.Aspire`, that delegates JSON body matching to UnitTestEx's own [`JsonElementComparer`](../src/UnitTestEx/Json/JsonElementComparer.cs) - via `WireMockServerSettings.CustomMatcherMappings`. This gives Tier 2/3 JSON body matching genuine parity with Tier 1 (semantic value coercion for dates, GUIDs and numbers), rather than being limited to WireMock.Net's own textual `JsonMatcher`/`JsonPartialMatcher`. It also drops the Docker/Podman requirement entirely - it's an ordinary .NET console app, so there's nothing to pull or run as a container. The official `WireMock.Net.Aspire` container resource (`AddWireMock`) remains fully supported for teams already standardized on that package - `AspireTesterBase.HttpMock` works against either resource type identically, since request matching is driven by the resource's admin API rather than its hosting mechanism.

Each mock resource is a genuine, isolated WireMock.Net process - one per external system, so stubs configured for `"email"` can never leak into `"auth"` or `"erp"`. Within a test, `AspireTesterBase.HttpMock(resourceName)` returns a fluent `AspireHttpMockClient` for the named resource:

``` csharp
await using var tester = AspireTester.Create<Projects.MyAppHost>();

await tester.WaitForResourceAsync("api");
await tester.WaitForResourceAsync("erp");

var stub = await tester.HttpMock("erp")
    .Request(HttpMethod.Get, "products/abc")
    .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

tester.Http("api")
    .Run(HttpMethod.Get, "Product/abc")
    .AssertOK()
    .AssertValue(new { id = "Abc", description = "A blue carrot" });

await stub.VerifyAsync();
```

The fluent configuration API is intentionally near-identical to Tier 1's `MockHttpClientFactory` above (both implement the shared [`IHttpMockClient`](../src/UnitTestEx/Mocking/IHttpMockClient.cs)/`IHttpMockRequest`/`IHttpMockResponse` interfaces) - a helper method written once against these interfaces can configure request/response stubbing identically regardless of which tier it's handed. The main differences are that the terminal `With*` methods here are asynchronous (`AspireHttpMockClient` performs a real HTTP call to the WireMock.Net server's admin API to register each mapping) and must be awaited, and the underlying JSON comparison/sequence-exhaustion semantics are WireMock.Net's own (not identical to Tier 1's) - unless you use `WithJsonBodyUsingUnitTestExComparer` instead of `WithJsonBody`. As with Tier 1, `Request`'s `requestUri` does not need a leading `/` - one is added automatically where absent, since WireMock.Net's admin API rejects a path that doesn't start with one:

``` csharp
var stub = await tester.HttpMock("erp")
    .Request(HttpMethod.Post, "orders")
    .WithJsonBodyUsingUnitTestExComparer(new { id = "Abc", occurredAt = "2024-01-01T00:00:00Z" }) // matches "2024-01-01T00:00:00.000+00:00" too - same instant, same as Tier 1
    .Respond.WithJsonAsync(new { id = "Abc", status = "Accepted" });
```

_Note:_ `WithJsonBodyUsingUnitTestExComparer` requires the target resource to have registered `JsonElementComparerMatcher` (i.e. a self-hosted resource per the template above) - the official `WireMock.Net.Aspire` container resource has no way to load a custom .NET matcher type. Where WireMock's own strict, non-semantic textual matching is instead wanted against a self-hosted resource, configure `JsonElementComparerOptions.ValueComparison` to `JsonElementComparison.Exact` rather than falling back to `WithJsonBody`.

The same shared interface also brings across Tier 1's [YAML/JSON configuration](./http-client-mocking.md#yamljson-configuration) - `await tester.HttpMock("erp").WithRequestsFromResourceAsync<MyTestClass>("my.mock.unittestex.yaml")` loads the exact same embedded resource schema against a real WireMock.Net resource.

### Pre-seeding stubs from `AppHost.cs`

`tester.HttpMock(resourceName)` requires an `AspireTester` - fine for tests, but not when a developer just wants to `aspire run`/`dotnet run` the `AppHost` directly (exploratory/manual use, no test in sight) - any un-stubbed external dependency will simply fail with a connection error. The `DistributedApplication.HttpMock(resourceName, endpointName?, jsonComparerOptions?)` extension method (also on `UnitTestEx.Aspire`, alongside `AddMockHostProject`/`WithMockHostEnvironment`) exposes the exact same fluent `AspireHttpMockClient` API directly against a started `DistributedApplication` - no tester required - so `AppHost.cs` itself can pre-seed sensible default stubs:

``` csharp
// AppHost.cs
var app = builder.Build();
await app.StartAsync();

await app.WaitForResourceAsync("erp");
await app.HttpMock("erp")
    .Request(HttpMethod.Get, "products/abc")
    .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });

await app.WaitForShutdownAsync();
```

This replaces the simpler `await app.RunAsync();` one-liner with the equivalent `StartAsync`/`WaitForShutdownAsync` pair (standard, supported Aspire usage) so there's a point after start-up, but before the host blocks, to seed mappings. A test can still layer its own stubs over these defaults (or `ResetAsync()` first to clear them) via `tester.HttpMock(...)` as normal - the AppHost's stubs are just a starting point, not a constraint on what a test may configure.

_Important:_ this pre-seeding code only ever runs for a real `aspire run`/`dotnet run` of the AppHost - it does **not** run under an `AspireTester`-driven test. `AspireTester`/`AspireTesterBase` build the AppHost via Aspire's own `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>()`, which intercepts the AppHost's `Program.cs` at `builder.Build()` and hands the (still-unbuilt) builder straight back to the test - none of `AppHost.cs`'s own code *after* that line (the `StartAsync`/`HttpMock`/`WaitForShutdownAsync` block above) is ever reached when driven via a test, so a test relying solely on it for a stub it actually depends on will fail with a genuine (not un-stubbed-but-otherwise-passing, and not flaky/racy) connection or 404 error every time. A test that needs the same stub must register it itself, most naturally via `BeforeStart`/`AfterStart` (the latter, once the mock host resource is actually up):

``` csharp
await using var tester = AspireTester.Create<Projects.MyAppHost>()
    .AfterStart(async app =>
    {
        await app.WaitForResourceAsync("erp");
        await app.HttpMock("erp")
            .Request(HttpMethod.Get, "products/abc")
            .Respond.WithJsonAsync(new { id = "Abc", description = "A blue carrot" });
    });
```

### Failing on unexpected resource error logs

A real inter-domain test can pass its own assertions while a background/hosted service (or a request that was never explicitly checked via `Http`) quietly logs an error or worse elsewhere in the `DistributedApplication` - `ErrorWhenLogContains` catches this by continuously watching every resource's forwarded log output for entries at or above a given `LogLevel` (`Error` by default) and failing the test the moment one appears:

``` csharp
await using var tester = AspireTester.Create<Projects.MyAppHost>()
    .ErrorWhenLogContains(exclude: ["*a known, benign warning*"]); // Optional: only needed to change the level, or add exclude/include patterns.

await tester.WaitForResourceAsync("shopping");

tester.Http("shopping").Run(HttpMethod.Get, "orders/123").AssertOK();

tester.Checkpoint("Final log check."); // Recommended: drains and checks any activity not otherwise seen by Http/Delay above.
```

This is **enabled by default** at `LogLevel.Error` for every `AspireTester` - there is no need to call `ErrorWhenLogContains` at all unless you want to change the `minimumLevel`, add wildcard (`*`/`?`) `exclude`/`include` patterns, or opt out entirely via `ErrorWhenLogContains(LogLevel.None)` (needed for a test that deliberately induces a resource-level error as its own subject, e.g. asserting a `500` response). `exclude` is the common case - known/expected noise that should not trigger a failure despite otherwise qualifying; a match here always wins. `include` is the rarer, opposite case - it *narrows* rather than widens what is checked, requiring an otherwise-qualifying entry to also match at least one `include` pattern to be reported (leave it `null`/empty, the default, to check every qualifying entry regardless of text). Each call fully **replaces** the prior configuration (level, `exclude` and `include` alike) rather than merging with it - pass the complete desired state each time. A resource log entry is only checked once *drained* - by `Checkpoint`, `Delay` or `Http` - so a trailing `Test.Checkpoint("Final log check.")` at the end of a test is recommended to also catch activity (e.g. shutdown-time logging) that nothing else happens to drain.

_Important:_ `include`/`exclude` only ever **narrow what counts as a violation** of the streaming check above - they are not a "was this text logged" positive assertion, and there is no aggregate, end-of-test tally of `include` patterns. If an `include` pattern never matches anything, the test simply never fails because of it; nothing confirms it was actually hit. For that - "assert this text *was* (or was *not*) logged, by any resource, at any point" - use `AssertLogContains`/`AssertLogNotContains` instead (below).

### Asserting expected resource log content

Unlike `ErrorWhenLogContains` above (a continuous check applied only as entries are subsequently *drained*), `AssertLogContains`/`AssertLogNotContains` are immediate, one-shot checks against **every** resource log entry captured for the lifetime of the test so far - drained or not, across every resource by default (or scoped to one via the optional `resourceName`):

``` csharp
await using var tester = AspireTester.Create<Projects.MyAppHost>();

await tester.WaitForResourceAsync("shopping");

tester.Http("shopping").Run(HttpMethod.Get, "orders/123").AssertOK();

tester.AssertLogContains("Order 123 processed successfully."); // Across every resource.
tester.AssertLogContains("Order 123 processed successfully.", "shopping"); // Scoped to just the 'shopping' resource.
tester.AssertLogNotContains("*unexpected*");
```

Because every resource log entry is retained for the whole test (not just what has been drained), there is no "did I check at the right moment" ambiguity to reason about - call this whenever (and as many times as) needed, typically right after whatever action is expected to have produced the entry; no trailing `Checkpoint` is required first (though calling it after one is perfectly fine too). Both accept the same wildcard (`*`/`?`), case-insensitive "contains" pattern matching as `ErrorWhenLogContains`'s `exclude`/`include`.

### Resetting captured logs

Every resource log entry captured is retained for the lifetime of the underlying `AspireTester` instance, not just a single test - normally a non-issue, since each test creates and disposes its own instance. If instead you deliberately **reuse** one `AspireTester`/`DistributedApplication` host across multiple tests (e.g. via a shared fixture, to avoid repeatedly paying its start-up cost), an earlier test's log activity would otherwise still be visible to a later test's `ErrorWhenLogContains`/`AssertLogContains`/`AssertLogNotContains` checks. Call `ResetLogs()` to discard everything captured so far - typically as the first line of such a test:

``` csharp
tester.ResetLogs(); // Discards all previously captured resource log entries (e.g. from a prior test sharing this host).

tester.Http("shopping").Run(HttpMethod.Get, "orders/123").AssertOK();

tester.AssertLogContains("Order 123 processed successfully."); // Only sees activity logged since the reset above.
```

This is deliberately explicit rather than automatic - unlike Tier 1, which resets its equivalent captured state automatically per Act (there being a single, well-defined Act boundary to hang it off), Tier 2/3 has no such boundary, and background/hosted-service activity occurring between calls is exactly what this capture exists to surface; an automatic reset would risk silently discarding it. `ResetLogs()` only clears previously captured log data - it does not reset any `ErrorWhenLogContains` configuration (level, `exclude`, `include`).

### Driving Azure Functions triggers and waiting for background work

Some Azure Functions triggers are delivered by the Functions host's HTTP webhook rather than by a normal HTTP endpoint. `FunctionsWebhook(resourceName)` resolves the resource's Aspire endpoint and can post Event Grid-schema events to either the blob-trigger route (`/runtime/webhooks/blobs`) or the Event Grid trigger route (`/runtime/webhooks/EventGrid`). The helper adds the `aeg-event-type: Notification` header and returns the raw response so the test can assert it:

```csharp
var created = EventGridExtensionMethods.CreateBlobCreatedEvent(
    "imports", blob.Name, blob.Uri, properties.ETag.ToString(), "text/csv", properties.ContentLength);

using var response = await tester.FunctionsWebhook("imports")
    .PostBlobCreatedAsync("Host.Functions.CustomerImport", created);
response.EnsureSuccessStatusCode();
```

`WaitForResourceAsync("functions")` only waits for Aspire's resource health check; an HTTP health endpoint can respond before the Functions isolated worker has finished indexing. Use `WaitForFunctionsAsync` when a test needs function registration to be ready:

```csharp
var functionNames = await tester.WaitForFunctionsAsync(
    "functions",
    expectedFunctionNames: ["CustomerImport", "HealthCheck"]);
```

This calls the Functions host's `/admin/functions` API, waits for the host to expose its indexed function list, and optionally verifies that the named functions are present. It does not inspect startup log text. A function omitted from the list (for example, one disabled by an unresolved `%SettingName%` binding expression) causes the expected-name check to fail. The method confirms function indexing, not successful connections to trigger services or end-to-end trigger delivery.

For a Blob Storage trigger configured with `Source = BlobTriggerSource.EventGrid`, distinguish **trigger execution** from **Event Grid delivery**:

- The Functions host receives the notification at its blob-trigger webhook, resolves the blob, applies the binding, invokes the function, and performs its output bindings. An Aspire test that posts to this webhook exercises those real host/runtime behaviors.
- Azurite emulates Blob Storage; it does **not** publish `Microsoft.Storage.BlobCreated` events or deliver them through an Event Grid subscription. The test must arrange delivery itself: upload the blob to Azurite, create the corresponding event, then post that event with `FunctionsWebhook`. This simulates the notification/delivery step, not the function execution.
- Consequently, this test does not verify Azure Event Grid subscription configuration, filtering, retries, or delivery semantics. Those require a real Azure Event Grid subscription or an Event Grid-compatible emulator that supports the desired scenario.

The repository's [Azurite integration test](../tests/UnitTestEx.Aspire.NUnit.Test/Functions/AzuriteBlobTriggerTest.cs) follows that split: it uploads a real blob, manually posts its BlobCreated notification to the running Functions host, then waits for and verifies a real output blob. By contrast, the in-process trigger tests under `UnitTestEx.NUnit.Test` invoke the function method directly and do not test host binding or notification delivery.

For an end-to-end import test, upload the blob and read the resulting report using the storage client and connection string already owned by the test/AppHost. `EventuallyAsync` polls a caller-supplied operation and returns its first result satisfying the condition; handle expected "not ready yet" outcomes (such as a 404 for a report that has not been written) in that operation. It does not depend on Azure Storage, so the same helper works for queues, databases, and other eventual side effects:

```csharp
var report = await tester.EventuallyAsync(
    async cancellationToken => await TryReadReportAsync(reportBlob, cancellationToken),
    value => value is not null,
    timeout: TimeSpan.FromSeconds(60));
```

The Event Grid builder lives in `UnitTestEx.Azure.Functions`; the webhook sender and generic polling helper are separate. UnitTestEx.Aspire does not manage Azurite, upload blobs, or add Azure Storage SDK dependencies. Configure the emulator/resource in the AppHost, then use the application's storage client to arrange and assert storage state.

The isolated AppHost for this proof starts Azurite and Azure Functions Core Tools without changing the existing container-free Aspire test host. Run it with `dotnet test tests/UnitTestEx.Aspire.NUnit.Test/UnitTestEx.Aspire.NUnit.Test.csproj --filter FullyQualifiedName~AzuriteBlobTriggerTest`; Podman and Azure Functions Core Tools v4 must be installed and available on `PATH`, and ports `7071` and `10000`-`10002` must be free.

### Service Bus emulator integration

The [Service Bus emulator test](../tests/UnitTestEx.Aspire.NUnit.Test/Functions/ServiceBusEmulatorTest.cs) starts a separate Functions host and an Aspire-managed Service Bus emulator. It sends a message to the emulator, lets the real Functions host receive and bind it, then verifies a message written to an output queue and checks the captured function log. This tests the host-to-broker integration; it does not validate Azure Service Bus delivery guarantees or every cloud feature.

The AppHost also starts the emulator's SQL Edge dependency and mounts its entity configuration. The emulator and SQL containers require a working Aspire-compatible container runtime (Podman locally; the GitHub-hosted CI runner provides Docker), and the first run downloads their images. Microsoft's emulator prerequisites list at least 2 GB RAM and 5 GB disk. The AppHost sets the emulator and SQL `ACCEPT_EULA` values because the repository's tests are accepting those terms for this test-only use. The emulator is for development/testing, not production, and is not a substitute for Azure smoke tests.

The resources are declared directly in the dedicated Aspire AppHost rather than maintained in a second `compose.yml`; the same integration test starts them locally and in CI. Run only this test with `dotnet test tests/UnitTestEx.Aspire.NUnit.Test/UnitTestEx.Aspire.NUnit.Test.csproj --filter FullyQualifiedName~ServiceBusEmulatorTest`.

_Note:_ Aspire-hosted resources are real OS processes (containers, where you opt into one), so `AspireTester` tests are inherently slower than the in-process Tier 1 testers - use them where the inter-process interaction itself is what needs proving.

_Note:_ Since Tier 2/3 resources are real, reachable URLs (not in-process fakes), a UI/frontend resource hosted in the `AppHost` can be driven directly with [Playwright](https://playwright.dev/dotnet/) - this is an ordinary consequence of the resources being real processes, not a UnitTestEx-specific feature; see Microsoft's own [Aspire + Playwright guide](https://learn.microsoft.com/en-us/dotnet/aspire/testing/write-your-first-test?tabs=xunit#creating-a-playwright-test) for the pattern.

<br/>


<br/>

## Examples

Capability | Example (xUnit; NUnit and MSTest equivalents live in [UnitTestEx.Aspire.NUnit.Test](../tests/UnitTestEx.Aspire.NUnit.Test) and [UnitTestEx.Aspire.MSTest.Test](../tests/UnitTestEx.Aspire.MSTest.Test))
-|-
Basic request through a real resource | [`Http_Get_ReturnsSuccess`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Resource environment / lifecycle hooks | [`WithResourceEnvironment_AppliedBeforeBuild`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`BeforeStart_InvokedInOrder_BeforeAnyResourceStarts`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`AfterStart_CanWaitForResourceHealthy_UsingExtensionMethod`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Log aggregation (Checkpoint/Delay) | [`Checkpoint_And_Delay_AggregatesResourceLogs`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Failing on resource error logs | [`ErrorWhenLogContains_DefaultsToEnabled_ThrowsWithoutExplicitRegistration`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`ErrorWhenLogContains_ExcludeWildcard_SuppressesOtherwiseViolatingEntry`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`ErrorWhenLogContains_ChecksOnFinalCheckpoint_ForActivityNotDrainedByHttp`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Asserting log content | [`AssertLogContains_FindsExpectedText_AcrossAllOrASpecificResource`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`AssertLogNotContains_ThrowsWhenTextFound`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`ResetLogs_DiscardsPreviouslyCapturedEntries`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Stubbing external dependencies (WireMock) | [`HttpMock_StubsExternalMockHostDependency_Product`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`HttpMock_WithSequenceAsync_ReturnsResponsesInOrderThenExhausts`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`HttpMock_WithJsonBody_PathsToIgnore_IgnoresSpecifiedProperty`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Shared stubs across tiers (IHttpMockClient, YAML) | [`HttpMock_SharedInterface_ConfiguresIdenticallyAcrossTiers`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`HttpMock_WithRequestsFromResourceAsync_LoadsStubsFromYaml_IdenticallyToTier1`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
UnitTestEx JSON comparer in a self-hosted mock | [`HttpMock_WithJsonBodyUsingUnitTestExComparer_SelfHostedMockHost_MatchesUsingJsonElementComparerSemantics`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
User / set-up flow | [`UseSetUp_And_UseUser_FlowThroughToHttpRequestSend`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)
Azure Functions blob webhook + Azurite | [`BlobCreatedWebhook_RunsFunction_AndWritesProcessedBlob`](../tests/UnitTestEx.Aspire.NUnit.Test/Functions/AzuriteBlobTriggerTest.cs)
Azure Functions Service Bus emulator | [`ServiceBusMessage_RunsFunction_AndWritesOutputQueue`](../tests/UnitTestEx.Aspire.NUnit.Test/Functions/ServiceBusEmulatorTest.cs)

[← Back to README](../README.md)
