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

One important wrinkle: `TesterBase<TSelf>` is the shared base a lot of fluent extension methods hang off
today, and most of its DI-flavoured methods (`ReplaceSingleton`, `MockScoped`, `ScopedType`, etc.) would
otherwise **silently no-op** rather than fail if called on a Tier 2 tester, because they queue into a
list that only Tier 1's `WebApplicationFactory` wiring ever plays back. Section 9 proposes a small,
additive, backward-compatible guard (`SupportsServiceConfiguration`) to turn that into a clear,
immediate `NotSupportedException` instead.

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

Illustrative only — not implemented by this doc. **Revised per section 9's inheritance analysis:**
`AspireTesterBase` extends the non-generic `TesterBase`, *not* `TesterBase<TSelf>` — see section 9 for
why.

```csharp
public abstract class AspireTesterBase<TAppHost, TSelf> : TesterBase, IAsyncDisposable
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
    // existing HttpTester/HttpTester<T> fluent assertion API for consistency with Tier 1 — made
    // possible because HttpTesterBase already only depends on the non-generic TesterBase (see
    // section 9), not on anything DI-related from TesterBase<TSelf>.
    public HttpTester Http(string resourceName);
    public HttpTester<TResponse> Http<TResponse>(string resourceName);

    // The two members TesterBase declares abstract; honestly unsupported for a multi-process tester.
    public override IServiceProvider Services =>
        throw new NotSupportedException("AspireTesterBase has no single in-process IServiceProvider; each resource owns its own DI container. Use Http(resourceName) or configure the resource directly instead.");
    public override IConfiguration Configuration =>
        throw new NotSupportedException("AspireTesterBase has no single in-process IConfiguration; configure/override the resource's own environment/configuration before it starts instead.");
}
```

A WireMock resource plugs into the same model as just another named resource: stub it via the thin
wrapper described in section 5, then assert against the real domain resource's behavior via
`Http(resourceName)` as usual.

## 9. Why `AspireTesterBase` inherits `TesterBase`, not `TesterBase<TSelf>`

**Revised conclusion, superseding an earlier draft of this section.** The first draft of this doc (and
the sketch in section 8) had `AspireTesterBase<TAppHost, TSelf> : TesterBase<TSelf>` — i.e. reuse the
*generic* base and guard away the parts that don't apply. On reflection that's the wrong shape: it's a
Liskov substitution violation waiting to happen. `TesterBase<TSelf>` (`src/UnitTestEx/Abstractions/TesterBaseT.cs`)
exists for exactly one reason — **in-process DI reach-in** — and it is *only* that: `ReplaceSingleton`/
`Scoped`/`Transient` + `Keyed`/`Mock` variants, `ReplaceHttpClientFactory`, `ScopedType<TService>`,
`Type<TService>` (~30 members total, all DI-flavoured). None of it applies to a multi-process Tier 2
tester, full stop. Inheriting it just to guard ~30 members with a runtime `NotSupportedException` means
every one of those methods *looks* like a valid, IntelliSense-suggested thing to call on an
`AspireTesterBase` — compiles clean, reads clean — and then blows up at test-run time. That's a bad
authoring experience for a large surface, and the guard from section 8 (now shipped) only mitigates it;
it doesn't remove it.

**What's actually shared between the tiers, concretely, not hypothetically:** the evidence is
`HttpTesterBase` (`src/UnitTestEx/AspNetCore/HttpTesterBase.cs`) — the class that already implements
Tier 1's entire HTTP request/response/assertion engine (request+response logging, JSON (de)serialization,
the `Expectations`/`Assertors` pipeline, per-request log capture correlated via `SharedState`/`RequestId`,
the `OnBeforeHttpRequestMessageSendAsync` user-impersonation hook). It takes an `Owner` typed as the
*non-generic* `TesterBase` in its constructor — **not** `TesterBase<TSelf>` — and never touches
`Services`, `Configuration`, `ConfigureServices`, or any `Replace*`/`ScopedType`/`Type` member. It's
composed alongside a `TesterBase<TSelf>`-derived `ApiTesterBase` today (which supplies the `TestServer`),
but it doesn't need the DI-heavy base itself. That's a real, working example — not a guess — of exactly
where the tier boundary actually sits: **`TesterBase` (non-generic) is the true shared capability set;
`TesterBase<TSelf>` is Tier-1-only.**

**What `TesterBase` (non-generic) actually gives you, and why every item is genuinely host-agnostic:**
- `Implementor`, `LoggerProvider`, `SharedState` — test-framework output plumbing and the log-capture/
  request-correlation mechanism `HttpTesterBase` relies on.
- `JsonSerializer`/`UseJsonSerializer`, `JsonComparerOptions`/`CreateJsonComparer` — test-side JSON
  handling, independent of the host model.
- `UserName`/`UseUser`/`WithUser` — feeds `TestSetUp.OnBeforeHttpRequestMessageSendAsync`, a hook that
  mutates the outgoing `HttpRequestMessage` before it's sent (e.g. to attach an OAuth token) — just as
  meaningful for a real cross-resource HTTP call in Tier 2 as an in-memory one in Tier 1.
- `SetUp` (`TestSetUp`), `Delay`, `ExecutePreRunActions`/`ExecutePostRun*Actions`,
  `OnHostStart`/`OnHostStartUp` — pipeline hooks `HttpTesterBase.SendAsync` already calls today;
  "host start" can be reinterpreted as "resource becomes healthy" for Tier 2.
- `ResetHost()` (`abstract`) — each tier already implements its own semantics; Tier 2's will look more
  like "tear down and rebuild the `DistributedApplication`" than Tier 1's in-place reset, but the shape
  (an abstract hook the base calls when needed) still fits.
- `Services`/`Configuration` (`abstract`) and `SupportsServiceConfiguration`/`ConfigureServices` (the
  guard shipped in this repo already) — `AspireTesterBase` still needs to override the two abstract
  members and still benefits from the guard as defense-in-depth (see below), but critically it does
  **not** need to inherit the ~30 concrete DI-flavoured methods to do so.

**What this means for `HttpTesterBase` itself (a small, real follow-up, not hand-waving):** today it's
hard-coded to `TestServer` (`CreateHttpClient() => new(new HttpDelegatingHandler(this,
TestServer.CreateHandler())) { BaseAddress = TestServer.BaseAddress }`). To let `AspireTesterBase.Http(resourceName)`
reuse the same engine, `HttpTesterBase`/its `HttpTester` subclasses need their `HttpClient` source
extracted behind a small seam (e.g. an abstract/virtual `CreateHttpClient()`, or a constructor taking a
`Func<HttpClient>` instead of a `TestServer` directly) so Tier 2 can supply `app.CreateHttpClient(resourceName)`
in place of `TestServer.CreateHandler()`. Everything else in `HttpTesterBase` — logging, JSON, expectations,
the user-impersonation hook — needs no change at all.

**So, concretely, for Phase 3 (prototyping `AspireTesterBase`):**
1. `AspireTesterBase<TAppHost, TSelf> : TesterBase` (non-generic) — see the revised section 8 sketch.
   It gets zero `Replace*`/`ScopedType`/`Type` members: not guarded away, simply never inherited. A
   consumer reading its IntelliSense sees only members that actually work.
2. Override `Services`/`Configuration` honestly (`throw new NotSupportedException(...)`, pointing at the
   real per-resource alternative) — this is a deliberate, single, well-documented override of two members
   already declared `abstract`, not a smell; it's exactly what an abstract member is for.
3. Extract `HttpTesterBase`'s `HttpClient` source behind a small seam so `Http(resourceName)` can reuse
   the entire existing HTTP assertion engine unchanged, fed from `app.CreateHttpClient(resourceName)`
   instead of `TestServer`.
4. The `SupportsServiceConfiguration` guard (already shipped in core `TesterBase`) remains valuable as
   defense-in-depth — not for `AspireTesterBase` itself (which never has the DI methods to call), but for
   *other* extension methods written directly against the non-generic `TesterBase` (by this repo, a
   companion package, or a consumer) that might reach into DI without going through the funnel. See below
   for the one existing entry point that still sits outside it.

Cataloguing the rest of what's on `TesterBase<TSelf>` for completeness, so nothing is assumed away:
`ScopedType<TService>`/`Type<TService>` (6 overloads) call `Services.CreateScope()` synchronously at the
call site — they'd already fail correctly once `Services` throws, if they were ever inherited, but under
the revised design they simply aren't present on `AspireTesterBase` at all, which is strictly better than
"present but throws."

**The actual gap the guard closes (still true, still shipped):** every `Replace*`/`Mock*`/
`ReplaceHttpClientFactory` method (~25 overloads) funnels through one method —
`TesterBase.ConfigureServices(Action<IServiceCollection>, bool)` — which was `protected` but **not
virtual** before this repo's change. A subclass had no way to intercept or reject it. Left unguarded,
calling e.g. `.ReplaceSingleton<IFoo>(...)` on any future non-in-process `TesterBase`-derived type would
compile fine, silently queue into an internal list, and then simply never run — because nothing in a
non-in-process flow ever calls `AddConfiguredServices(IServiceCollection)`. **That's a silent no-op, not
a compile error or a runtime exception** — worse than an explicit exception. The guard fixes that for any
type that ends up with those methods in scope; the revised `AspireTesterBase` design avoids needing to
rely on it at all by simply not inheriting them, but the guard is still the right belt-and-braces fix at
the `TesterBase` level, in case a future extension method (companion package or consumer) adds its own
`Replace*`-shaped helper directly against `TesterBase`.

```csharp
// TesterBase — public getter (read-only, virtual) so extension methods — UnitTestEx's own, a companion
// package's, or a consumer's — can defensively check the capability themselves, not just rely on
// catching the exception. Defaults to true, so every existing Tier 1 tester needs zero changes.
public virtual bool SupportsServiceConfiguration => true;

protected void ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
{
    if (!SupportsServiceConfiguration)
        throw new NotSupportedException(
            $"{GetType().Name} does not support in-process service configuration/replacement because its " +
            $"underlying host does not run in-process (see {nameof(SupportsServiceConfiguration)}). " +
            $"Instead, configure the target resource through its supported environment/configuration surface " +
            $"(e.g. an Aspire resource builder's 'WithEnvironment'/'WithReference'), or mock its external HTTP " +
            $"dependencies at the resource boundary (e.g. using WireMock.Net.Aspire) rather than in-process.");

    lock (SyncRoot)
    {
        if (autoResetHost)
            ResetHost(false);

        _configureServices.Add(configureServices);
    }
}
```

Making the getter `public` rather than `protected` matters because `TesterBase<TSelf>`'s whole design
point is being a hook for extension methods (many of `UnitTestEx`'s own fluent methods are already
written as extensions elsewhere in the codebase, and consumers/companion packages write their own).
A `protected` flag is invisible to any of those — they'd have no way to guard themselves and would just
propagate whatever exception the guarded core method throws (or, worse, do the DI-touching work
*themselves* without ever routing through `ConfigureServices`, bypassing the guard entirely). A public
getter lets any extension method written against `TesterBase<TSelf>` check
`tester.SupportsServiceConfiguration` up front and either skip the operation, throw its own
tier-appropriate message, or offer a fallback — the same pattern already used for the public
`IsHostInstantiated` flag on `TesterBase`.

`AspireTesterBase` doesn't even need to override `SupportsServiceConfiguration` — under the revised
design (section 9 above) it never inherits `TesterBase<TSelf>` or its ~25 DI-flavoured fluent methods in
the first place, so there's no funnel call site to guard for it specifically. It still implements the
already-abstract `Services`/`Configuration` to throw. The guard earns its keep for any *other*
`TesterBase`-derived type (present or future) that does end up with DI-touching methods in scope without
going through the intended flow.

This is intentionally a single boolean switch, not a `[Flags]` capability enum — given how centralized
`ConfigureServices` already is and how few other members are actually host-model-sensitive (see above),
a richer capability model would be speculative complexity today. If further, more granular gaps emerge
once `UnitTestEx.Aspire` is actually built, a `TesterCapabilities` flags enum can replace the single
public property then, without another breaking change (the guarded call site stays the same shape).

**Existing extension methods audited too, not just core members.** `TesterBase`/`TesterBase<TSelf>` is
also the hook for extension methods defined outside `UnitTestEx` itself, so the companion packages were
checked as well:

- `UnitTestEx.Azure.ServiceBus/ExtensionMethods.cs` (`CreateServiceBusMessageFromValue`,
  `CreateServiceBusMessage*`, etc.) and `UnitTestEx.Azure.Functions/ExtensionMethods.cs`
  (`CreateWebJobsServiceBusMessageActions`, etc.) both extend `TesterBase` — but neither touches DI at
  all; they only read `tester.JsonSerializer`/`tester.Implementor` to build message payloads/assertors.
  Both are host-agnostic already and need no guarding.
- `UnitTestEx/ExtensionMethods.cs`'s `ReplaceSingleton`/`ReplaceScoped`/`ReplaceTransient`/`Keyed*`/
  `Remove`/`RemoveKeyed` are extensions on `IServiceCollection`, not on `TesterBase` — they can only ever
  be called from inside a `ConfigureServices(sc => ...)` callback delegate. Since that callback is only
  ever queued (and only ever played back) through the now-guarded `TesterBase.ConfigureServices`, these
  are automatically shielded for free: for a Tier 2 tester the callback is never queued in the first
  place (the guard throws before it can be), so these `IServiceCollection` extensions are simply never
  invoked. No separate check needed inside them.

The public `SupportsServiceConfiguration` getter still matters for *future* extension methods —
particularly anything a `UnitTestEx.Aspire` package itself, or a consumer, might add directly against
`TesterBase` that touches DI without going through the existing `ConfigureServices` funnel. Any such
method should check `tester.SupportsServiceConfiguration` itself rather than assume the funnel will catch
it, exactly because today's audit shows the funnel is the only enforcement point.

**One direct configuration entry point deliberately sits outside the guard: `TestSetUp.ConfigureServices`.**
This is a global `Action<IServiceCollection>?` delegate (set once on `TestSetUp`/`TestSetUp.Default`,
not per-tester) that each *concrete* Tier 1 tester's own host-building code invokes directly —
e.g. `ApiTesterBase`/`GenericTesterCore` both call `SetUp.ConfigureServices?.Invoke(sc)` immediately
before `AddConfiguredServices(sc)` while constructing their `IServiceCollection`. It never routes through
`TesterBase.ConfigureServices`, so the new guard clause does not — and cannot — see it. This is safe by
construction rather than by the guard: an `AspireTesterBase`'s host-building code has no in-process
`IServiceCollection` to build in the first place, so it simply would never call
`SetUp.ConfigureServices?.Invoke(...)` at all — there is nothing to guard because there is no call site
to guard. This is called out explicitly so it isn't mistaken for a gap the `SupportsServiceConfiguration`
guard is responsible for closing; it is a different, tester-implementation-level entry point, and each
future Tier 2 tester implementation is simply responsible for not invoking it.

## 10. Versioning and CI impact

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

## 11. Recommendation

Ship this as a new, separately-versioned, **opt-in** `UnitTestEx.Aspire` package, following the same
companion-package pattern already established by `UnitTestEx.Azure.Functions` and
`UnitTestEx.Azure.ServiceBus` — the core `UnitTestEx` package and `ApiTesterBase`/`MockHttpClientFactory`
are untouched either way. The small, backward-compatible changes needed in core `UnitTestEx` are the
`SupportsServiceConfiguration` guard (already shipped) and the `HttpTesterBase` `HttpClient`-source
extraction (section 9) — everything else is additive. Suggested phasing for follow-up work:

1. This design doc (done).
2. Add the `SupportsServiceConfiguration` guard to `TesterBase` (section 9) — a small, additive,
   backward-compatible core change, landed and released independently of the rest (done).
3. Extract `HttpTesterBase`'s hard-coded `TestServer`/`HttpClient` construction behind a small seam
   (section 9) so it can be fed from either `TestServer` (Tier 1, unchanged) or
   `app.CreateHttpClient(resourceName)` (Tier 2) — a small, additive, backward-compatible core change,
   ideally landed and released independently too, ahead of the rest.
4. Prototype `AspireTesterBase<TAppHost, TSelf> : TesterBase` (non-generic — see revised section 8/9)
   with resource-scoped `Http()`/`Http<T>()` (reusing the extracted engine from step 3),
   `WaitForResourceAsync`, and environment-override helpers.
5. Thin WireMock.Net wrapper mirroring `MockHttpClient`'s authoring syntax.
6. Documentation for the Playwright/UI pattern (section 7) — no new code required, just guidance.
