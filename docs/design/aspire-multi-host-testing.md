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
today, and its DI-flavoured methods (`ReplaceSingleton`, `MockScoped`, `ScopedType`, etc.) would be
awkward — even dangerous — on a Tier 2 tester if it simply inherited that surface: they'd compile, look
valid, and either silently no-op or need a runtime guard to catch them. Section 9 works through this and
lands on a **compile-time** fix instead of a runtime one: extract a new `TesterBaseCore` holding only the
genuinely host-agnostic members, have today's `TesterBase` inherit it unchanged (Tier 1's shape doesn't
move), and have the new Tier 2 base inherit `TesterBaseCore` directly — so the DI-flavoured methods
simply don't exist on a Tier 2 tester, rather than existing and throwing.

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

### 5.1 `HttpMock` implementation notes (finalized during build-out)

Two semantics divergences from WireMock.Net's raw defaults were deliberately closed to keep the `HttpMock`
authoring experience consistent with Tier 1's `MockHttpClientFactory`:

- **Sequenced responses (`WithSequenceAsync`) exactly mirror Tier 1**: the sequence length is the *exact*
  expected invocation count (not a minimum, not silently repeating the last response forever) — the same
  as `MockHttpClientRequest`'s `Responses` mode. `.Times(...)` is disallowed in combination with
  `WithSequenceAsync` (throws `InvalidOperationException`) because it's meaningless once the sequence
  itself is the exact-count contract. An invocation beyond the configured sequence returns a distinct
  `500 InternalServerError` from a synthetic guard mapping (WireMock.Net can't throw a .NET exception
  across the process boundary the way Tier 1 can from inside the mocked call), and `VerifyAsync()` checks
  both that guard was never hit and that every configured response was invoked exactly once.
- **`WithJsonBody(..., pathsToIgnore: ...)` uses WireMock.Net's native `JsonPartialMatcher`**, not
  UnitTestEx's own `JsonElementComparer` — matching happens inside the (potentially separate-process)
  WireMock.Net server, which has no way to call back into the test process's comparer. Named paths are
  stripped from the pattern before it's posted, then the matcher switches from strict `JsonMatcher` to
  `JsonPartialMatcher`. This is deliberately simpler than Tier 1's `pathsToIgnore` (dot-separated property
  names only, no JSONPath/array indices) and *looser*: `JsonPartialMatcher` also silently tolerates any
  other unanticipated extra property in the actual body, not just the ones explicitly ignored. Good enough
  for the common case (an unpredictable ETag/timestamp/GUID) but callers should know it isn't a drop-in
  equivalent of Tier 1's stricter bidirectional-deep-equal-minus-ignored-paths behavior. Left unchanged
  (strict `JsonMatcher`) when `pathsToIgnore` isn't supplied.

### 5.2 Shared `IHttpMock*` interfaces — write the stubbing code once, use it on either tier

Tier 1's `MockHttpClient*` (Moq-based, in-process) and Tier 2/3's `AspireHttpMock*` (WireMock.Net-based,
out-of-process) both implement a common set of interfaces in `src/UnitTestEx/Mocking/`:
`IHttpMockClient`, `IHttpMockRequest`, `IHttpMockRequestBody`, `IHttpMockResponse`,
`IHttpMockResponseSequence`, `IHttpMockResponseSequenceItem`, and `IHttpMockedRequest`. A helper method
written once against `IHttpMockClient` (e.g. `HttpMockSharedConfig.ConfigureProductStubAsync` in the test
suites) configures request/response stubbing identically regardless of which concrete tier is passed in —
only the `Times`/JSON-comparison/sequence-exhaustion *semantics* remain tier-specific (see 5.1 above); the
authoring surface itself is unified.

Each interface is deliberately kept to a minimal, irreducible core of abstract members; every convenience
overload (a plain-text `WithBody(text)`, a raw-JSON-string `WithJsonAsync(json)`, the embedded-resource
`WithJsonResource*` variants, `Headers(...)`, an integer-millisecond `Delay(int)`, etc.) is a C# default
interface method (DIM) composed purely from those core members, so neither tier has to reimplement them.
A DIM is only reached when called through a variable of the *interface* type — calling a same-named method
directly on the concrete class invokes that class's own native method instead, an entirely separate code
path. `tests/UnitTestEx.Xunit.Test/HttpMockInterfaceTest.cs` exercises every DIM extra via
interface-typed variables (shared bytecode, so Tier-1-only coverage proves both tiers); a corresponding
Aspire test (`HttpMock_InterfaceCoreMembers_TierSpecificAdaptations` in `AspireTesterTest.cs`) exercises
the *core* members' hand-written, tier-specific explicit interface implementations instead (the 2-arg
`WithBody`, `WithJsonBody<T>`, both branches of the `WithAsync` content/no-content adaptation, and the
`Action<IHttpMockResponseSequence>`-wrapping adapter inside `WithSequenceAsync`).

Retrofitting Tier 1 onto this shared surface surfaced one genuine cross-tier semantic gap:
**`WithAnyBody()`**. Aspire's native implementation adds zero WireMock.Net body constraints — a true
wildcard, matching a request with or without a body. Tier 1's native implementation, however, required
`request.Content != null` before matching, so it could never match a body-less request (e.g. a `GET`) —
inconsistent with both Aspire's behaviour and the interface's own documented contract ("matches regardless
of the body content"). Tier 1 was changed to also be a true wildcard, aligning it with Aspire; this was a
deliberate, user-approved behavior change to an existing native Tier 1 API (not just new interface-only
code), so any test previously relying on "a body-less request after `WithAnyBody()` still fails to match"
needed updating (see `UriAndAnyBody`/`DefaultHttpClient` in the Tier 1 test projects).

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

Illustrative only — not implemented by this doc. **Revised per section 9's analysis:** `AspireTesterBase`
extends the new `TesterBaseCore` (not `TesterBase`, and not `TesterBase<TSelf>`) — see section 9 for why.

```csharp
public abstract class AspireTesterBase<TAppHost, TSelf> : TesterBaseCore, IAsyncDisposable
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
    // possible because HttpTesterBase depends only on TesterBaseCore plus the new IHttpClientSource
    // seam (see section 9), not on anything DI-related from TesterBase/TesterBase<TSelf>.
    public HttpTester Http(string resourceName);
    public HttpTester<TResponse> Http<TResponse>(string resourceName);

    // Implements the new seam so HttpTesterBase can source an HttpClient for a named resource
    // instead of a TestServer (see section 9).
    HttpClient IHttpClientSource.CreateHttpClient(string? name) =>
        GetDistributedApplicationAsync().GetAwaiter().GetResult().CreateHttpClient(name ?? throw new ArgumentNullException(nameof(name)));
}
```

Note what's conspicuously *absent* here: no `Services`, no `Configuration`, no `NotSupportedException`
overrides at all. `AspireTesterBase` never inherits those members in the first place (they live on
`TesterBase`, which it doesn't extend) — so there's nothing to throw away or guard. A consumer's
IntelliSense on an `AspireTesterBase`-derived tester simply doesn't show `Services`/`Configuration`/
`ReplaceSingleton`/etc. — the tier boundary is enforced by the compiler, not by a runtime exception.

A WireMock resource plugs into the same model as just another named resource: stub it via the thin
wrapper described in section 5, then assert against the real domain resource's behavior via
`Http(resourceName)` as usual.

## 9. `TesterBaseCore`: extracting the genuinely shared base

**Revised conclusion, superseding two earlier drafts of this section.** The doc has now gone through two
iterations on this point, each closer to the truth:
1. First draft: `AspireTesterBase<TAppHost, TSelf> : TesterBase<TSelf>` — reuse the generic DI-heavy base
   and guard away the parts that don't apply. Wrong — see point 2.
2. Second draft: `AspireTesterBase : TesterBase` (non-generic) — better, but still meant overriding the
   two abstract `Services`/`Configuration` members to throw, and still relied on a runtime
   `SupportsServiceConfiguration` guard (shipped in this repo, now reverted — see below) as a safety net
   for anything else on `TesterBase` that might reach into DI.
3. **This draft:** extract a new abstract `TesterBaseCore`, containing only the members that are
   genuinely host-agnostic. `TesterBase` (unchanged in shape from before this doc's changes) inherits it.
   `AspireTesterBase` inherits it directly, **skipping `TesterBase` entirely**. This isn't just "one fewer
   level of inheritance" — it means `AspireTesterBase` never has `Services`, `Configuration`,
   `ConfigureServices`, or any DI-flavoured member to override, guard, or explain away. There's nothing to
   throw because there's nothing to call. The tier boundary is enforced by what the compiler will let you
   write, full stop — not by a runtime flag someone has to remember to check.

**Why this is better than the runtime guard, concretely:** the guard's entire premise was "a Tier 2
tester will end up with ~25 DI-flavoured methods it can't honor, so let's make them throw a clear
exception instead of silently no-opping." That premise assumed Tier 2 *would* inherit those methods. Once
`AspireTesterBase` simply doesn't — because it never extends the class that declares them — the premise
disappears, and with it the reason for the guard to exist. Keeping a public `SupportsServiceConfiguration`
property and a runtime check in `ConfigureServices` around after that would be exactly the kind of
speculative infrastructure the rest of this doc argues against: a flag that, in every real subclass that
will ever exist, can only ever be `true`. **The guard has been reverted from `TesterBase`** (it shipped
in this branch's PR before this revision; removing it is a clean, zero-impact change since the PR was
never released).

**What's actually shared between the tiers, concretely, not hypothetically:** the evidence is
`HttpTesterBase` (`src/UnitTestEx/AspNetCore/HttpTesterBase.cs`) — the class that already implements
Tier 1's entire HTTP request/response/assertion engine (request+response logging, JSON (de)serialization,
the `Expectations`/`Assertors` pipeline, per-request log capture correlated via `SharedState`/`RequestId`,
the `OnBeforeHttpRequestMessageSendAsync` user-impersonation hook). It takes an `Owner` typed as
`TesterBase` today, but never touches `Services`, `Configuration`, `ConfigureServices`, or any
`Replace*`/`ScopedType`/`Type` member — everything it actually uses is a genuinely host-agnostic member.
That's real, working evidence — not a guess — of exactly where the tier boundary sits.

**`TesterBaseCore` — the proposed member split, cross-checked against every member currently on
`TesterBase`:**

| Stays agnostic → moves to `TesterBaseCore` | Stays DI-specific → stays on `TesterBase` |
|---|---|
| `Implementor`, `LoggerProvider`, `SharedState`, `SetUp` | `Configuration` (`abstract`) |
| `UserName`/`AdditionalConfiguration` (user-impersonation, config overrides) | `Services` (`abstract`) |
| `JsonSerializer`, `JsonComparerOptions`, `CreateJsonComparer()` | `ConfigureServices(Action<IServiceCollection>, bool)` |
| `IsHostInstantiated`, `SyncRoot`, `ResetHost()` (abstract hook) | `AddConfiguredServices(IServiceCollection)` |
| `OnHostStart`/`OnHostStartUp` | the backing `_configureServices` list |
| `PreRunActions`/`PostRunBeforeExpectationsActions`/`PostRunAfterExpectationsActions`/`PostRunActions` + `Execute*Actions` | |
| `ReplaceTestFrameworkImplementor`, `LogHttpResponseMessage` | |
| all `CreateHttpRequest*`/`CreateJsonHttpRequest*` overloads (pure request-DTO builders, no DI touched at all) | |

One mechanical wrinkle: today's public `ResetHost(bool resetConfiguredServices = false)` both resets
`IsHostInstantiated` (agnostic) *and* clears `_configureServices` (DI-specific) in one method. Splitting
the class means splitting this too: `TesterBaseCore` keeps a parameterless `ResetHost()` that resets
`IsHostInstantiated` and calls the abstract reset hook; `TesterBase` keeps the existing
`ResetHost(bool resetConfiguredServices)` overload, which additionally clears `_configureServices` before
calling `base.ResetHost()`. Existing Tier 1 callers see no change — same overload, same behavior.

**The `IHttpClientSource` interface — the concrete answer to "what's common but doesn't fit the base
class":** `HttpTesterBase.CreateHttpClient()` is hard-coded to `TestServer.CreateHandler()` today. Rather
than adding a Tier-2-aware branch inside `HttpTesterBase` itself, or a virtual method that only makes
sense for one tier, the cleaner seam is a small interface:

```csharp
public interface IHttpClientSource
{
    // name is the resource/client name for Tier 2 (e.g. Aspire's app.CreateHttpClient(name)) and
    // ignored (or unused) for Tier 1, which always has exactly one TestServer to source from.
    HttpClient CreateHttpClient(string? name = null);
}
```

`ApiTesterBase` (Tier 1) implements it by wrapping its existing `TestServer`; `AspireTesterBase` (Tier 2)
implements it by wrapping `app.CreateHttpClient(name)` (see the section 8 sketch). `HttpTesterBase` takes
an `IHttpClientSource` instead of reaching for a `TestServer` field directly, and calls
`Source.CreateHttpClient(resourceName)` where it currently calls `TestServer.CreateHandler()`. This is
exactly the pattern the discussion raised: a capability that's genuinely shared in *concept* between the
tiers, but whose *implementation* is unavoidably tier-specific, doesn't belong crammed onto the base
class (as a field/virtual method that only half the hierarchy can sensibly implement) — it belongs on a
small, focused interface that each tier's concrete tester implements on its own terms, and that
composed/consumed code (`HttpTesterBase`, future extension methods) depends on instead of a concrete
base type. Everything else in `HttpTesterBase` — logging, JSON, expectations, the user-impersonation
hook — needs no change at all; only the `HttpClient`-sourcing line moves behind this interface.

**So, concretely, for Phase 3 (prototyping `AspireTesterBase`):**
1. Extract `TesterBaseCore` per the table above; `TesterBase : TesterBaseCore` (Tier 1's public shape is
   unchanged — this is a pure "extract base class" refactor, source- and binary-compatible for existing
   consumers).
2. Introduce `IHttpClientSource`; have `ApiTesterBase` implement it (wrapping its existing `TestServer`)
   and have `HttpTesterBase` depend on the interface instead of a concrete `TestServer` field.
3. `AspireTesterBase<TAppHost, TSelf> : TesterBaseCore, IHttpClientSource, IAsyncDisposable` — see the
   revised section 8 sketch. Zero `Services`/`Configuration`/`Replace*`/`ScopedType`/`Type` members: not
   guarded away, simply never inherited.
4. `Http(resourceName)`/`Http<T>(resourceName)` reuse `HttpTesterBase`/`HttpTester`/`HttpTester<T>`
   unchanged, fed via `AspireTesterBase`'s `IHttpClientSource` implementation.

**Existing extension methods, re-audited against the new split.** `UnitTestEx.Azure.ServiceBus`'s and
`UnitTestEx.Azure.Functions`' extension methods on `TesterBase` only read `JsonSerializer`/`Implementor`
— both now on `TesterBaseCore` — so they're unaffected either way. `UnitTestEx/ExtensionMethods.cs`'s
`ReplaceSingleton`/`ReplaceScoped`/etc. are extensions on `IServiceCollection`, only ever invoked from
inside a `ConfigureServices(sc => ...)` callback — a callback that, for `AspireTesterBase`, is never
queued in the first place because `ConfigureServices` doesn't exist on the type it's calling through.
No runtime check needed anywhere in this chain.

**`TestSetUp.ConfigureServices` — the one direct configuration entry point, now trivially out of reach
rather than merely unguarded.** This is a global `Action<IServiceCollection>?` delegate (set once on
`TestSetUp`/`TestSetUp.Default`, not per-tester) that each *concrete* Tier 1 tester's own host-building
code invokes directly — e.g. `ApiTesterBase`/`GenericTesterCore` both call
`SetUp.ConfigureServices?.Invoke(sc)` immediately before `AddConfiguredServices(sc)` while constructing
their `IServiceCollection`. Under the previous (guard-based) draft this was called out as "safe by
construction, not by the guard, because there's no in-process `IServiceCollection` to build." Under this
draft that's even more clearly true: `AspireTesterBase`'s host-building code is written against
`TesterBaseCore`, which doesn't have `AddConfiguredServices`/`SetUp.ConfigureServices` plumbing to call in
the first place — there is no call site to reach, guarded or otherwise.

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
are untouched in observable behavior either way. The small, backward-compatible changes needed in core
`UnitTestEx` are the `TesterBaseCore` extraction and the new `IHttpClientSource` interface (section 9) —
everything else is additive. Suggested phasing for follow-up work:

1. This design doc (done).
2. Extract `TesterBaseCore` from `TesterBase` per the member split in section 9 — a small,
   backward-compatible "extract base class" refactor (Tier 1's public shape is unchanged); introduce
   `IHttpClientSource` and have `ApiTesterBase`/`HttpTesterBase` implement/depend on it instead of a
   hard-coded `TestServer` field. Land and release this independently, ahead of the rest.

   *(Note: an earlier draft of this doc shipped a runtime `SupportsServiceConfiguration` guard on
   `TesterBase` as a first step instead. That guard has been reverted — the `TesterBaseCore` split
   makes it structurally unnecessary: `AspireTesterBase` never inherits the DI-flavoured members it
   would have guarded, so there was nothing left for it to protect.)*
3. Prototype `AspireTesterBase<TAppHost, TSelf> : TesterBaseCore` (see revised section 8/9) with
   resource-scoped `Http()`/`Http<T>()` (reusing the existing HTTP engine via `IHttpClientSource` from
   step 2), `WaitForResourceAsync`, and environment-override helpers.
4. Thin WireMock.Net wrapper mirroring `MockHttpClient`'s authoring syntax.
5. Documentation for the Playwright/UI pattern (section 7) — no new code required, just guidance.

