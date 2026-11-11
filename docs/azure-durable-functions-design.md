# Azure Durable Functions testing design

**Status:** Phase 0 activity baseline implemented; Phase 1 engine spike not started.
**Scope:** Azure Functions v4 isolated worker on .NET 8, 9, 10, and 11; no in-process/WebJobs support.

## Outcome and proof boundary

Start with a normal Durable Functions application, representative orchestration bodies, and direct tests of its activities through the existing `FunctionTester`. This deliverable is a **sample, regression tests, and usage documentation**, not new Durable testing infrastructure. It remains useful if orchestration support is deferred.

The next decision is whether UnitTestEx should offer a scripted context, adapt Microsoft's in-memory `DurableTaskTestHost`, or provide complementary runners. Activity invocation proves neither orchestration behavior nor runtime indexing/binding. A real Functions host/backend belongs in separate Aspire integration tests.

Keep application startup ordinary: no custom `Program` class, `BuildHost`, or test-only entry point. The CoreEx standalone Durable Task SDK workflow is a possible future consumer, not a reason to commit to shared public packages now.

## Phase 0: sample and activity baseline

The application is [`UnitTestEx.DurableFunction`](../tests/UnitTestEx.DurableFunction/Program.cs); tests are [`DurableActivityTest`](../tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs). It uses the official Durable extension, currently 1.22.0, with normal top-level startup. No UnitTestEx dependency exists in the sample app.

The current tester supports synchronous value/void and `Task`/`Task<T>` methods. Static calls work because invocation uses `MethodInfo.IsStatic`, not the expression receiver. A static class cannot be a generic argument, but an ordinary application marker works with the existing API:

```csharp
using var test = FunctionTester.Create<WorkflowActivities>();
test.Trigger<WorkflowActivities>()
    .Run(_ => ActivityTransforms.NormalizeReference(" order-1 "))
    .AssertSuccess()
    .AssertValue("ORDER-1");
```

`WorkflowActivities` locates the app assembly; the static method's own metadata supplies its trigger validation. No non-generic trigger overload is needed for this baseline.

| Activity | Existing tester coverage |
|---|---|
| `NormalizeReference` | Static class, synchronous scalar result, invalid/null input |
| `TotalItems` | Static synchronous collection input and aggregate result |
| `ProcessItem` | Instance `Task<ItemResult>`, DTO, DI construction, `FunctionContext`, captured logging, invalid input |
| `RecordWorkflow` | Instance synchronous void, injected service interaction |
| `PublishWorkflow` | Instance `Task`, DTO input, asynchronous injected service completion |
| `RejectWorkflow` | Static `Task<string>`, original asynchronous activity exception |

Derive orchestration fixtures from the spike's questions, rather than authoring every possible workflow:

- `BatchOrchestration`: normalization chain, three same-name item calls, fan-out/fan-in, ordered aggregate, retry options, custom status, replay-safe logging, time/IDs, and a finalization child.
- `ApprovalOrchestration`: external event versus durable timer, application-supplied duration (including 1-hour/24-hour cases), cancellation of the losing wait.
- `FailureOrchestration`: caught activity failure and a separately invokable propagated-failure body; child failure behavior can use that body in the spike.

These are shared fixtures for later execution tests, not three more coverage matrices. Timers, external events, and fan-out are orchestration patterns, not activity trigger variants. Deliberately invalid awaits belong in later negative-test fixtures.

**Phase 0 acceptance:** every sample activity has a direct test through `Trigger<T>()`; normal startup/DI works without worker startup or backend connection. Exceptions remain original activity exceptions, not simulated `TaskFailedException`. Sample compilation and generated function metadata are not evidence of orchestration execution. No public API change is required.

## Engine decision and package boundary

**Current hypothesis:** both engines may be useful: a fake for controlled hour/day timer scenarios, and the host for real engine history/replay. First try to disprove the need for the fake. A supported host clock facility meeting the virtual-time requirement would favor host-only. A host wrapper adding no value beyond direct SDK use should not become a package.

**Initial package preference:** one new `UnitTestEx.Azure.Functions.Durable` package, if implementation is justified. Keep any host-neutral harness internal and structured for extraction, not exposed as a speculative `UnitTestEx.DurableTask` API. Extract a shared public core only when a second actual consumer requires it. This avoids moving already-public types later.

If both engines are justified, do not make fake users inherit the host dependency graph: keep the host prototype as direct SDK usage until its wrapper earns a separate optional package, provisionally `UnitTestEx.Azure.Functions.Durable.TestHost`. The general `UnitTestEx.Azure.Functions` package must not acquire Durable dependencies. Exact cross-package contracts remain a spike outcome.

The upstream test-host project references Durable Task Core, gRPC server/client, Protobuf, and SDK gRPC client/worker projects. Confirm the selected published NuGet graph and release maturity; upstream `main` is not a compatibility guarantee. Publish dependency minimums, not upper caps; validate minimum/current supported versions and all target frameworks.

### Capability matrix

SDK facilities are not implemented UnitTestEx features. "Prototype" means feasibility remains unproven. Two engines must have explicit selection and distinct control APIs.

| Capability | In-memory host | Scripted fake |
|---|---|---|
| Functions/class-based bodies | SDK delegate/class registration; Functions services/context adaptation is a prototype | Invocation/context adapter prototype |
| Activity/child outcomes and matching | Registered stub delegates plus recording/verification adapter | Scripted result/failure and strict matching prototype |
| Fan-out/fan-in and controlled completions | Gated stubs, bounded asynchronous observation | Synchronous pump and selected activity/child completions |
| Timers/event races | Real engine timers/client events; no documented virtual clock found yet | `AdvanceBy`/`AdvanceTo`, event delivery and cancellation inspection prototype |
| Replay | Real engine history; episode coverage must be demonstrated | Optional self-replay research, not runtime replay |
| Serialization/failures | SDK behavior; Functions compatibility must be compared | Explicit boundary snapshots/failure details prototype |
| Invalid awaits | Engine checks with documented diagnostic limits | Provenance/ownership research; no universal detector promised |
| Lifetime | Asynchronous client/worker, in-process gRPC listener, explicit disposal | Synchronous steps/results, no listener |
| Functions host/backend | Neither engine proves indexing, binding, or production backend behavior | Aspire/runtime tests required |

### Why wrap the host?

A wrapper must demonstrate benefits over direct SDK use: bridge `FunctionTester` DI scopes and captured logging, supply optional `FunctionContext`, adapt static/instance Functions expressions, integrate UnitTestEx value/failure assertions, and own bounded waits/disposal. Do not start the Functions worker merely to obtain these services.

Illustrative **host-only** sketch, not a committed API:

```csharp
await using var run = await test.DurableHost()
    .RegisterOrchestrator(ctx => BatchOrchestration.ProcessBatch(ctx))
    .RegisterOrchestrator<BatchOrchestration>((f, ctx) => f.FinalizeBatch(ctx))
    .RegisterActivity<WorkflowActivities>((f, item, fc) => f.ProcessItem(item, fc))
    // Register the remaining production activities or explicit stub delegates.
    .StartAsync(cancellationToken);
var id = await run.Client.ScheduleNewOrchestrationInstanceAsync(
    nameof(BatchOrchestration.ProcessBatch), request, cancellation: cancellationToken);
var result = await run.WaitForResultAsync<BatchResult>(id, timeout, cancellationToken);
result.AssertSuccess().AssertValue(expected);
```

Registration overloads, input type inference, and exact client signatures must be checked against the selected SDK. Registered stubs provide scripted outcomes and record serialized inputs/counts. Host tests use client events and status/history; they do not expose `RunUntilBlocked`, per-timer completion, or pretend to advance time. Without a supported clock, exercise short real timers, not silently rewritten production delays.

## Phase 1: decision-first feasibility spike (on hold)

**Proposed budget:** five engineer-days for a decision, not a promise to implement nine gates across two engines. Phase 0 is separate. Owner: TBD.

1. **Day 1, host kill probes:** pin a published host package; register a real Functions body and required stubs; check services/context/serialization adaptation. Inspect public APIs/source for a supported clock facility. Test clock behavior, not just the presence of a `TimeProvider` type. If the host meets virtual-time needs, skip fake research.
2. **Days 2-3, selected branch:** if host invocation works but supported virtual time does not, prototype only the minimum fake needed for ordered fan-in, failure details, and virtual timers. Probe invalid-await limits early; abandon a general deterministic-pump claim if escapes cannot be diagnosed. If host invocation itself fails, establish whether a small adapter solves it before spending the remaining budget.
3. **Day 4, evidence gaps:** compare the decision-critical fixtures on viable engines; sample the required follow-on capabilities below. Fake self-replay is optional research, not a parallel engine project.
4. **Day 5, decision:** record pass/fail/unproven evidence, dependency graph, measured timer behavior, remaining work, and the smallest viable public scope. Do not treat unproven capabilities as supported.

### Tiered evidence checklist

**Decision-critical:** failure prevents selection for that capability; complementary engines may cover different capabilities. Prefer fewer engines when one covers the required use cases.

| Gate | Concrete evidence |
|---|---|
| Invocation | Static/instance Functions bodies execute without app restructuring/private reflection/backend connection. Phase 0 supplies activity evidence; class-based body adaptation is a follow-on reuse probe. |
| Matching/fan-in | Three same-name calls matched by typed inputs complete C/B/A; pending after C/B; output retains task-list order. Unexpected and unused scripted calls fail diagnostically. |
| Serialization/failures | Mutating caller objects cannot alter boundary snapshots/results. DTO/enum settings are exercised. `TaskFailedException.FailureDetails.IsCausedBy<T>()`, type/message and nested causes match an executed SDK failure. Record Functions serializer differences. |
| Virtual time | Compare the same 1-hour and 24-hour timer fixture over repeated warm runs: no duration-proportional sleeping; the 24-hour median is within the larger of twice the 1-hour median or the 1-hour median plus measured no-op pump overhead. Record timer firings/clock values. Do not use a fixed five-second CI performance assertion. Missing clock support makes the host ineligible for virtual-time control, not unusable for all testing. |

**Required follow-on, not engine-decision blockers:** a failed/unproven probe narrows the initial supported surface and creates an explicit implementation item. It does not discard passing decision-critical evidence. Only tested capabilities can be advertised; no universal fidelity claim is made for a narrower release.

| Area | Probe and consequence |
|---|---|
| Cancellation | Timer/event wait tokens, timeout overloads, pre-canceled tokens, losing-timer cleanup, bounded harness disposal. Do not invent activity/child cancellation. Unproven cases remain unsupported. |
| Sub-orchestrations | Script child outcomes/verify options; host registers a real child. Keep nested-orchestration claims out until proven, rather than making basic activity orchestration wait on them. |
| Replay | Force multiple engine episodes with a gated activity; verify normal replay. For a negative fixture, an isolated test-only static counter changes the activity name on each body entry; register both names so failure is history divergence, not missing registration. Use a fresh host, reset counter, bounded wait, and verify a nondeterminism diagnostic. No public history injection is assumed. If caching prevents a replay, record the gap rather than claim this test passed. |
| Invalid awaits | Probe `WhenAny(Task.Delay(...), WaitForExternalEvent(...))`, direct I/O, `Task.Run`, and `ConfigureAwait(false)`. Record what ownership/provenance guards catch and what escapes. An empty durable-pending set or a timeout is not a complete detector. Narrow fake validity claims if reliable provenance is infeasible. |
| Dependencies/compatibility | Check published package graph, framework support and isolation. Restore/type incompatibilities block the affected package release, not all other fixture research. |

## Proposed invocation and behavior contract

Function-based invocation validates the expression's method and `[OrchestrationTrigger]` parameter. Support `(f, ctx) => f.Run(ctx)`, `(_, ctx) => OrderOrchestration.Run(ctx)`, and a context-only overload for static classes. Do not instantiate static function types. Class-based `TaskOrchestrator<TIn,TOut>.RunAsync(ctx,input)` needs a separate adapter, not generated-attribute assumptions.

Arrange orchestration input with `.WithInput(request)`; activity setup inputs are separate. Use `OnActivity`/`OnSubOrchestrator` for setup and `AssertActivityCalled` for verification. Strict matching is unordered by default, with explicit counts and failure on unexpected/unused setup. Call records retain serialized snapshots and expose deserialized inspection copies typed by registration/arrangement.

Serialization occurs at durable boundaries, not arbitrary local calls. **Input identity is adapter-specific:** the SDK `TaskOrchestrationContextWrapper.GetInput<T>()` returns its retained typed object; the Functions wrapper returns an already-compatible input or uses an input converter which deserializes for each conversion call. Match the selected Functions path; do not universally mandate once-per-episode identity.

Failures use serialized `TaskFailureDetails` and a production-shaped `TaskFailedException`, not the original activity exception. The public exception-taking constructor populates details through `TaskFailureDetails.FromException` but retains the original exception; a fake should round-trip the details and use the details-taking constructor. Preserve type/message/nullable stack/nested failures; do not fabricate a stack for an exception never thrown. Compare observable details against the pinned SDK worker.

Cancellation means timer/event waits and test lifecycle. Canceling a wait or terminating a parent does not cancel activities/child instances. Capture status, replay-safe logs, context time/IDs, and retry **configuration**; a fake does not prove runtime retry execution.

**Compatibility:** generic `Trigger<T>()` currently accepts orchestration/entity attributes and can be used with caller mocks. Preserve that behavior now. Rejection is not committed; if proposed later, it requires a major version of the existing Functions package, release notes, and an error linking to optional-package guidance even when not installed. Entities remain outside the new tester's scope.

## Later phases and Aspire

- **Phase 2, smallest runner:** implement only the engine/API justified by Phase 1; retain Phase 0 activity tests, add supported orchestration expressions/arrangements and starter/status-response examples using caller-mocked `DurableTaskClient`.
- **Phase 3, expanded interactions:** add child orchestration, cancellation edge cases, replay and diagnostic capabilities as their evidence matures; maintain the capability matrix and explicit unsupported-operation failures.
- **Phase 4, release:** public docs/assertions, dependency/framework matrix, and package isolation. Standalone worker/backend integration, entities, `ContinueAsNew`, and durable HTTP are not initial claims.

In parallel, reuse the sample with a **separate opt-in** Aspire AppHost/test project: Core Tools plus Azurite/Azure Storage provider, indexing readiness, client scheduling, bounded status/output polling and cleanup. Use a lower test-only `extensions.durableTask.storageProvider.maxQueuePollingInterval` (for example one second; default 30 seconds). Do not add emulator requirements to the existing general/container-free host.

`AspireTester`, `WaitForFunctionsAsync`, HTTP requests and `EventuallyAsync` should suffice initially. Real starter/status URLs and Functions binding/serialization belong here. A scheduler-emulator backend can follow once Storage/Azurite is stable. Emulators do not prove Azure authentication, production scale or cloud reliability.

## Appendix: conditional fake design notes

These details matter only if the fake is selected; none is an implemented API or a host capability.

- Use a synchronous `RunUntilBlocked()` step returning pending/completed state and a synchronous result. Assert pending after two of three activity completions; after the third, assert completion/output. Keep activity/child completion handles, not manual timer completion.
- Default virtual epoch: 2000-01-01T00:00:00Z, overridable. `AdvanceTo`/`AdvanceBy` fire timers in `(dueTime,schedulingSequence)` order, draining continuations between firings, including newly due timers. Never rewind; reject negative advances/non-UTC targets. Same-time event/timer delivery follows explicit test action order. Bound pump steps for zero-time loops.
- Fail a completed run with live uncanceled timers. The host may instead remain running; bounded status/history assertions must not call that success.
- A controlling context/scheduler can check ownership on context calls and terminal transitions, and tag durable completions. It cannot universally intercept arbitrary await provenance or thread-pool escapes. Report measured limits; no private task inspection or IL rewriting.
- Optional self-replay history needs scheduling IDs, boundary payloads/failure details, completion delivery sequence, cancellations, event arrival/delivery timestamps, timer due/fire timestamps, and episode/clock transitions. Release completions in that recorded order/time, not all immediately. Verify unchanged fan-out/races before claiming divergence detection.
- `IsReplaying` covers the replayed prefix and changes at new work; a global true flag is not replay. Self-replay detects trace divergence, not every I/O/side effect, and is not Azure runtime replay. Recommend the Durable Task Roslyn analyzer independently.

## Sources and current observations

- [Microsoft unit-testing guidance](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-unit-testing): checked 2026-11-11; Functions pivot uses mocks/static direct invocation, SDK pivot lists the test-host prerequisite. Combined-page prerequisites do not establish a Functions-host adapter.
- [Test-host README](https://github.com/microsoft/durabletask-dotnet/blob/main/src/InProcessTestHost/README.md) and [project dependencies](https://github.com/microsoft/durabletask-dotnet/blob/main/src/InProcessTestHost/InProcessTestHost.csproj): public registration and dependency evidence, not proof of Functions serializer/clock compatibility.
- [SDK context wrapper](https://github.com/microsoft/durabletask-dotnet/blob/main/src/Worker/Core/Shims/TaskOrchestrationContextWrapper.cs), [Functions wrapper](https://github.com/Azure/azure-functions-durable-extension/blob/dev/src/Worker.Extensions.DurableTask/FunctionsOrchestrationContext.cs), and [input converters](https://github.com/Azure/azure-functions-durable-extension/blob/dev/src/Worker.Extensions.DurableTask/FunctionsOrchestrationContext.InputConverter.cs): different `GetInput<T>()` paths; recheck selected package versions.
- [`TaskFailedException`](https://github.com/microsoft/durabletask-dotnet/blob/main/src/Abstractions/TaskFailedException.cs) and [`TaskFailureDetails`](https://github.com/microsoft/durabletask-dotnet/blob/main/src/Abstractions/TaskFailureDetails.cs): public construction and mapping; validate pinned-package behavior.
- [Durable analyzer](https://learn.microsoft.com/en-us/azure/azure-functions/durable-functions/durable-functions-roslyn-analyzer), [`host.json` settings](https://learn.microsoft.com/en-us/azure/azure-functions/durable-functions/durable-functions-host-json-settings), and [scheduler quickstart](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/quickstart-durable-task-scheduler).
- Existing UnitTestEx [Functions](./azure-functions.md) and [Aspire](./aspire.md) documentation define the invocation/runtime proof boundaries.
