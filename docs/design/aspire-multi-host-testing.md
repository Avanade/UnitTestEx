# Design note: supporting .NET Aspire multi-host testing alongside `WebApplicationFactory`

## TL;DR

Yes, it's possible — but it's **two separate tiers**, not one API extended to cover both:

| | Tier 1 — intra-domain (today) | Tier 2 — inter-domain (proposed) |
|---|---|---|
| Package | `UnitTestEx` (existing) | new `UnitTestEx.Aspire` |
| Underlying host | `WebApplicationFactory<TEntryPoint>` + in-memory `TestServer` | `Aspire.Hosting.Testing.DistributedApplicationTestingBuilder` |
| Process model | Single in-memory process | Real, separate OS processes per resource |
| Transport | In-memory short-circuit (no sockets) | Real network / service discovery |
| DI reach-in | Full — `ConfigureServices`, `ReplaceScoped`, `ReplaceSingleton` | None — DI containers are in other processes |
| Use it when... | You want deep, per-component control of **one** service in isolation | You want to exercise the **real** interaction between two or more services |

This isn't a UnitTestEx-specific compromise — it matches Aspire's own guidance verbatim:

> "If your goal is to test a single project in isolation, run components in-memory, or mock external
> dependencies, consider using `WebApplicationFactory<T>` instead."
> — [aspire.dev/testing/overview](https://aspire.dev/testing/overview/)

So the plan is to keep Tier 1 exactly as-is, and add Tier 2 as a new, separately-versioned, opt-in
package — picking the tier per test based on what you're actually trying to prove, not blending them.

## 1. The current model (Tier 1), recapped

`ApiTesterBase<TEntryPoint, TSelf>` (`src/UnitTestEx/AspNetCore/ApiTesterBase.cs`) wraps a
`WebApplicationFactory<TEntryPoint>`:

- One process, one in-memory `TestServer` — `Server.CreateClient()`/`TestServer` has no real socket.
- `ConfigureServices` runs inside the *same* DI container the app under test uses, so tests can
  `ReplaceScoped`/`ReplaceSingleton` any component (repository, clock, feature flag, etc.) with a test
  double, per test, with zero ceremony.
- Outbound `HttpClient` calls are intercepted via `MockHttpClientFactory`
  (`src/UnitTestEx/Mocking/MockHttpClientFactory.cs`), which replaces the singleton
  `IHttpClientFactory` in that same DI container with a Moq-backed fake. Requests are matched
  (method/URL/headers/body) and canned responses returned — entirely in-process, no real HTTP.
- `appsettings.unittest.json` + `AdditionalConfiguration` layer test-only configuration in.

This is fast (no process spin-up, no network), fully deterministic, and gives complete component-level
control — but it can only ever exercise **one** service's pipeline at a time. Anything the service calls
outside itself (another domain's API, a payment gateway) is necessarily a mock, never the real thing.

## 2. Why Aspire changes the rules

`Aspire.Hosting.Testing`'s `DistributedApplicationTestingBuilder` starts the **entire AppHost** —
every project, container, and executable resource it declares — as **separate real processes**,
wired together with Aspire's real service discovery, exactly as they'd run in production:

```csharp
var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MyAppHost>();
await using var app = await appHost.BuildAsync();
await app.StartAsync();

await app.ResourceNotifications.WaitForResourceHealthyAsync("shopping", cts.Token);
using var httpClient = app.CreateHttpClient("shopping");
```

Consequences that matter for test design:

- **No cross-process DI.** You cannot `ConfigureServices`/`ReplaceScoped` into another process's
  container. Aspire's own docs are explicit about this: *"Because Aspire tests run services in separate
  processes, you can't inject services directly through dependency injection. However, you can
  influence application behavior through environment variables or configuration."* Overrides go through
  `appHost.CreateResourceBuilder<ProjectResource>("name").WithEnvironment(...)`, or `WithExplicitStart`
  and config-conditional resource registration in the AppHost itself — not through an in-memory DI hook.
- **Real network.** Requests actually leave one process's socket and arrive at another's Kestrel
  listener — real serialization, real middleware pipeline, real service-discovery resolution.
- **Higher cost.** Process start-up, health-check waits, and (for container resources) Docker pulls make
  these tests orders of magnitude slower than an in-memory `TestServer` round-trip.

None of this is a limitation to "work around" — it's precisely what makes Tier 2 valuable: it's the only
way to prove that two real services actually talk to each other correctly.

## 3. The two-tier model — pick one per test, don't force a hybrid

- **Tier 1 stays exactly as it is.** If the point of a test is "does the Shopping service correctly
  handle a `ProductPriceChanged` event, with the repository/clock/feature-flag mocked out precisely" —
  that's a single-service, deep-DI-control test. Use `ApiTesterBase` directly. No amount of Aspire
  wrapping replaces this, because the DI reach-in it relies on simply isn't available across process
  boundaries.
- **Tier 2 (new `UnitTestEx.Aspire`) is for the case where the point of the test *is* the real
  interaction between two or more domains.** If you actually want to prove Shopping's real HTTP call to
  Products resolves correctly, hits the real routing/model binding/middleware in Products, and gets a
  real response back — that can only be proven by running both as real processes.
- A hybrid (Tier-1-hosted service running alongside Tier-2 resources, with Aspire's resolved endpoints
  fed into a `WebApplicationFactory`'s configuration) is *technically* possible, but it's a narrow,
  advanced escape hatch — not something to design the mainline API around. Keep it out of scope for the
  initial `UnitTestEx.Aspire` package; document it later only if a real need shows up.

## 4. Worked scenario: Shopping + Products calling an external mail/payment gateway

This is the shape of test the user described, mapped onto the model above:

1. **Shopping and Products are the "key internals" under test** → run both as real Aspire project
   resources under Tier 2. A request into Shopping that triggers a real call into Products exercises
   the actual contract between them — the thing Tier 1 can never prove.
2. **The mail/payment gateway is a true external 3rd-party boundary**, not something you own or want to
   hit for real in a test → swap it for a **`WireMock.Net.Aspire`** resource in the AppHost, and point
   Shopping's/Products' configuration at the WireMock endpoint instead of the real one:

   ```csharp
   var gateway = builder.AddWireMock("payment-gateway"); // WireMock.Net.Aspire resource

   builder.AddProject<Projects.Shopping>("shopping")
       .WithReference(gateway)
       .WithEnvironment("PaymentGateway__BaseUrl", gateway.GetEndpoint("http"));
   ```

   In the test, stub the gateway's responses through WireMock's server API (real HTTP, matched by
   route/method/body) before asserting on Shopping's/Products' behavior.

## 5. Is WireMock the equivalent of UnitTestEx's `MockHttpClientFactory`?

**Conceptually, yes** — both let you declare "when a request matching X comes in, respond with Y" and
verify the expected calls happened. **Mechanically, they're different tools solving the same intent at
different layers**:

| | `MockHttpClientFactory` (Tier 1) | WireMock.Net (Tier 2) |
|---|---|---|
| What it replaces | `IHttpClientFactory` inside one process's DI container | Nothing — it's a real HTTP server |
| Reachable from | Only the process it's registered in | Any process on the network — that's the whole point |
| Transport | In-memory (`DelegatingHandler`), no socket | Real HTTP, in-proc host or container |
| Works across Aspire processes? | No | Yes |

So for Tier 2, WireMock.Net (via the official `WireMock.Net.Aspire` package) *is* the cross-process
counterpart of what `MockHttpClientFactory` does in-process for Tier 1. To keep the authoring experience
consistent, `UnitTestEx.Aspire` should ship a thin fluent wrapper over WireMock.Net's stub API, mirroring
the existing `MockHttpClient`/`MockHttpClientRequest`/`MockHttpClientResponse` builder syntax as closely
as possible, so switching between tiers doesn't mean learning a new mocking DSL.

## 6. Mocking individual components case-by-case across processes

Since there's no cross-process DI, "mock this one component in that other service" has to be solved
without reaching into its container. Options, in order of preference:

1. **Environment-variable/config-driven test hooks baked into each project's own `Program.cs`** — e.g. a
   convention like the existing `appsettings.unittest.json`, where a project opts in to swapping a
   component when a specific config value/env var is set. This is a per-project responsibility (a
   documented convention UnitTestEx can recommend), not something injected externally.
2. **Swap the whole resource for a stub** — if you don't need the real implementation at all, replace it
   in the AppHost with a WireMock resource or a minimal stand-in project, exactly as in the gateway
   example above.
3. **Use Tier 1 directly** — if what you actually need is deep DI control of one specific service, that's
   a sign the test's real goal is single-service isolation, not multi-host integration. Write it as a
   Tier 1 test instead of contorting Tier 2 to fake DI reach-in it was never designed to have.

## 7. The Playwright/UI angle

If the AppHost grows to include a web frontend (Blazor, React, etc.), Tier 2 resources are real,
reachable URLs — so [Playwright can drive them directly](https://learn.microsoft.com/dotnet/aspire/fundamentals/testing/web-testing/playwright/),
an officially documented Microsoft pattern, not something UnitTestEx needs to build:

```csharp
await app.ResourceNotifications.WaitForResourceHealthyAsync("frontend", cts.Token);
var frontendUrl = app.CreateHttpClient("frontend").BaseAddress;

await using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();
var page = await browser.NewPageAsync();
await page.GotoAsync(frontendUrl!.ToString());
```

This is a natural extension of Tier 2, not a third tier: the AppHost already orchestrates the frontend as
a real process, so a real browser can just point at it. Contrast with Tier 1, where `TestServer` has no
real socket — Playwright has nothing to connect to. `UnitTestEx.Aspire` doesn't need its own Playwright
wrapper; a documentation section showing this pattern (resource → real URL → Playwright) is enough.

## 8. Sketch of the new `UnitTestEx.Aspire` package surface

Illustrative only — not implemented by this doc:

```csharp
public abstract class AspireTesterBase<TAppHost, TSelf> : TesterBase<TSelf>, IAsyncDisposable
    where TAppHost : class
    where TSelf : AspireTesterBase<TAppHost, TSelf>
{
    // Builds + starts the DistributedApplicationTestingBuilder for TAppHost on first access,
    // mirroring ApiTesterBase's lazy GetWebApplicationFactory() pattern.
    protected Task<DistributedApplication> GetDistributedApplicationAsync();

    // Config/env overrides applied before BuildAsync (the only override surface available
    // across process boundaries).
    public TSelf WithResourceEnvironment(string resourceName, string key, string value);

    // Waits for a resource to be healthy/running before issuing requests against it.
    public Task WaitForResourceAsync(string resourceName, TimeSpan? timeout = null);

    // Resource-scoped Http()/Http<T>() wired to the resource's real HttpClient, reusing the
    // existing HttpTester/HttpTester<T> fluent assertion API for consistency with Tier 1.
    public HttpTester Http(string resourceName);
    public HttpTester<TResponse> Http<TResponse>(string resourceName);
}
```

A WireMock resource plugs into the same model as just another named resource: stub it via the thin
wrapper described in section 5, then assert against the real domain resource's behavior via
`Http(resourceName)` as usual.

## 9. Versioning and CI impact

- Aspire requires **.NET 8+**. `UnitTestEx.Aspire` would target `net8.0;net9.0;net10.0` only — it cannot
  support `net6.0`/`net7.0` the way the core `UnitTestEx` package currently does.
  `Aspire.Hosting.Testing` is the driving dependency.
- The test project needs a `<ProjectReference>` to the AppHost project — `DistributedApplicationTestingBuilder.CreateAsync<T>()`
  requires a `Projects.*`-namespace type that's only source-generated for referenced AppHost projects.
  This is a structural requirement on consumers, not something UnitTestEx can abstract away.
- CI runners need **Docker** available for any container-backed resource (WireMock container variant,
  service bus/DB emulators). Tier 1 has no such requirement today.
- **Startup latency** is much higher — process spin-up and health-check waits per resource, vs. an
  in-memory `TestServer`. Expect Tier 2 suites to run substantially slower and probably to be a smaller,
  more targeted subset of the overall test suite than Tier 1 suites.
- **Log/trace capture** across multiple real processes is harder than today's single in-process
  `ILoggerProvider` capture (`LoggerProvider.CreateLogger(...)` in `ApiTesterBase`) — Aspire's dashboard/
  OTLP pipeline is the natural place to look, rather than trying to replicate today's single-process log
  capture across processes.

## 10. Recommendation

Ship this as a new, separately-versioned, **opt-in** `UnitTestEx.Aspire` package, following the same
companion-package pattern already established by `UnitTestEx.Azure.Functions` and
`UnitTestEx.Azure.ServiceBus` — the core `UnitTestEx` package and `ApiTesterBase`/`MockHttpClientFactory`
are untouched either way. Suggested phasing for follow-up work:

1. This design doc (done).
2. Prototype `AspireTesterBase`/`DistributedTesterBase` with resource-scoped `Http()`/`Http<T>()`,
   `WaitForResourceAsync`, and environment-override helpers.
3. Thin WireMock.Net wrapper mirroring `MockHttpClient`'s authoring syntax.
4. Documentation for the Playwright/UI pattern (section 7) — no new code required, just guidance.
