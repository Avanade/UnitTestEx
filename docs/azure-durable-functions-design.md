# Azure Durable Functions testing design

**Status:** Day 1-5 feasibility decision recorded; non-packable source-project prototypes now include the narrow context helper, fluent SDK-host facade, optional host diagnostics, and opt-in activity invocation verification. Runtime probes remain isolated under `tests` and have only been executed on net10.
**Scope:** Azure Functions v4 isolated worker on .NET 8, 9, 10, and 11; no in-process/WebJobs support.

## Outcome and proof boundary

The Phase 0 contribution remains a normal Durable Functions sample and direct activity tests through the existing `FunctionTester`. The feasibility spike has now moved its narrow context helper into a non-packable source-project prototype, with runtime probes still under `tests`; it is not a shipped package or production testing infrastructure. The sample and activity tests remain useful if orchestration support is deferred.

The feasibility result favors complementary runners: Microsoft's in-memory `DurableTaskTestHost` for real SDK engine behavior where virtual time is unnecessary, and a narrow context adapter for method-level timer/event tests. Activity invocation proves neither orchestration behavior nor runtime indexing/binding. A real Functions host/backend belongs in separate Aspire integration tests.

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

## Desired fluent testing experience

**Design goal, not an implemented API:** retain UnitTestEx's fluent Arrange -> Run -> Assert experience, ordinary application startup, captured logs, and useful failure diagnostics. Engine selection must be explicit, but should not force users to hand-wire SDK infrastructure for routine tests. The spike must assess this experience as well as engine mechanics; method signatures and supported capabilities remain provisional.

The sketches below use a **conditional scripted runner**, named `DurableScripted` to distinguish it from `DurableHost`. They do not select the fake in advance or imply that host registration can provide scheduler controls. The host sketch below shows its distinct asynchronous lifecycle; a host wrapper should offer comparable fluent arrangements/assertions where supported, without pretending to offer virtual time.

**Scope after the feasibility decision:** the broad `DurableScripted` examples below are exploratory design sketches, not part of the selected minimum package. The selected method-level path is limited to virtual timers/events and explicit unsupported-operation failures; controlled activity completion, a deterministic pump, and a self-replay fake remain out of scope.

### Successful instance orchestration

Using the real `FinalizeBatch` body, arrange the void/`Task` activities and assert the returned value. Each setup in these examples expects one call; unexpected or unused setups fail. `Completes()` means successful completion without a result.

```csharp
using var test = FunctionTester.Create<WorkflowActivities>();
var expected = new BatchResult("ORDER-1", 2m, [new ItemResult("A", 2m)]);

test.DurableScripted<BatchOrchestration>()
    .WithInput(expected)
    .OnActivity<string>(nameof(WorkflowActivities.RecordWorkflow), value => value == expected.Reference)
        .Completes()
    .OnActivity<BatchResult>(nameof(WorkflowActivities.PublishWorkflow), value => value.Reference == expected.Reference)
        .Completes()
    .Run((function, context) => function.FinalizeBatch(context))
    .AssertSuccess()
    .AssertValue(expected);
```

`Run` is a synchronous convenience for scenarios whose arranged operations can complete immediately. An unresolved durable wait must produce a diagnostic directing the test to `Start` and explicit stepping, not hang. Static bodies use a context-only expression as below, or the `(function, context) => StaticFunction.Run(context)` form without instantiating a static class.

### Activity failure and orchestration recovery

`Fails` describes an activity failure at a durable boundary, not a direct throw into the orchestration. The runner must construct production-shaped `TaskFailedException.FailureDetails`, allowing the real `IsCausedBy<WorkflowRejectedException>()` catch filter to execute.

```csharp
test.DurableScripted<FailureOrchestration>()
    .WithInput("order-1")
    .OnActivity<string, string>(nameof(ActivityTransforms.RejectWorkflow), value => value == "order-1")
        .Fails(new WorkflowRejectedException("Rejected"))
    .Run(context => FailureOrchestration.RecoverWorkflow(context))
    .AssertSuccess()
    .AssertValue("Declined");
```

### Controlled fan-out/fan-in: scripted-runner capability

Use the real `ProcessBatch` body, completing three same-name activities in C/B/A order. Typed predicates select setups; typed call records select the specific pending invocation. A `Returns(call => ...)` callback receives a call record whose `Input` is a deserialized inspection copy typed by its arrangement.

```csharp
var request = new BatchRequest("order-1", [new("A", 1m), new("B", 2m), new("C", 3m)]);
var expected = new BatchResult("ORDER-1", 12m, [new("A", 2m), new("B", 4m), new("C", 6m)]);
var script = test.DurableScripted<BatchOrchestration>()
    .WithInput(request)
    .OnActivity<string, string>(nameof(ActivityTransforms.NormalizeReference), value => value == request.Reference)
        .Returns(expected.Reference)
    .OnActivity<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), _ => true)
        .Returns(expected.Total)
    .OnSubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), _ => true)
        .Returns(call => call.Input);
foreach (var item in request.Items)
    script.OnActivity<WorkItem, ItemResult>(nameof(WorkflowActivities.ProcessItem), value => value.Id == item.Id)
        .RemainsPending();

using var run = script.Start(context => BatchOrchestration.ProcessBatch(context));
run.RunUntilBlocked().AssertPending();
var calls = run.ActivityCalls<WorkItem>(nameof(WorkflowActivities.ProcessItem));
foreach (var id in new[] { "C", "B" })
{
    run.CompleteActivity(calls.Single(call => call.Input.Id == id), expected.Items.Single(item => item.Id == id));
    run.RunUntilBlocked().AssertPending();
}
run.CompleteActivity(calls.Single(call => call.Input.Id == "A"), expected.Items[0]);
run.RunUntilBlocked().AssertCompleted();
run.Result.AssertSuccess().AssertValue(expected);
```

The result must retain A/B/C task-list order despite C/B/A completion. The child is scripted here, not executed; a separately registered real child is a different test. `RemainsPending`, selected completions and synchronous stepping require a scheduler-capable runner. A host would use gated activity stubs and bounded asynchronous observation instead.

### Virtual time and an external-event race: scripted-runner capability

Exercise the production 24-hour timeout without shortening it or sleeping. Deliver approval halfway through, then verify the losing timer was canceled by the orchestration.

```csharp
using var run = test.DurableScripted<ApprovalOrchestration>()
    .WithInput(new ApprovalRequest(TimeSpan.FromHours(24)))
    .Start(context => ApprovalOrchestration.AwaitApproval(context));
run.RunUntilBlocked().AssertPending();
run.AdvanceBy(TimeSpan.FromHours(12));
run.RunUntilBlocked().AssertPending();
run.RaiseEvent("Approved", new ApprovalDecision(true));
run.RunUntilBlocked().AssertCompleted();
run.Result.AssertSuccess().AssertValue(new ApprovalDecision(true));
run.AssertTimerCanceled();
```

A separate timeout case advances to 24 hours and expects `ApprovalDecision(false)`, with the event wait canceled. Only `AdvanceBy`/`AdvanceTo` fire timers, in due order; there is no manual per-timer completion. These controls are not host capabilities unless Phase 1 proves supported clock integration.

## Engine decision and package boundary

**Selected direction (design only; no production implementation or publication yet):** pursue a minimal opt-in Durable testing package with two explicitly separate test paths. The current evidence supports complementary value: the SDK host exercises real engine history/replay but has wall-clock timers; a narrow public-context helper can test real method-level timer/event coordination with virtual time but has no engine replay.

| Optional package | Minimum responsibility | Explicit boundary |
|---|---|---|
| `UnitTestEx.Azure.Functions.Durable` (provisional name) | A narrow method-level context helper for virtual timers, external events, input, and cancellation inspection. Keep it independent of the test-host package. | No activity/child scripting, general fake, replay, serialization simulation, Functions worker, or backend. Unsupported context operations fail explicitly. |
| `UnitTestEx.Azure.Functions.Durable.TestHost` (provisional name) | A thin wrapper around Microsoft's in-memory test host for selected real SDK orchestration execution/replay, with UnitTestEx DI/logging/context bridges, result diagnostics, bounded waits, and disposal. A context decorator forwards calls and warns on wall-clock timers. | Optional dependency graph; no virtual-clock claim, no Functions worker/binding/backend fidelity, and no timer override that bypasses the engine. Keep prerelease while based on the preview host package. |

The existing `UnitTestEx.Azure.Functions` package must not acquire Durable dependencies. Do not expose a speculative host-neutral public core or a general `UnitTestEx.DurableTask` API; extract shared public types only when a second actual consumer requires them. Package names, exact APIs, and target-framework support remain provisional pending implementation and compatibility checks.

**Prototype boundary:** the source projects are currently non-packable and are not release artifacts. The context project contains the narrow virtual-time context. The host project starts the SDK host with the FunctionTester's captured logger factory and adds the registry helper that warns about real-time timers. Activity scopes, `FunctionContext` creation, orchestration registration, result assertions, and bounded waits remain caller-owned; this is not a finished package API.

The upstream test-host project references Durable Task Core, gRPC server/client, Protobuf, and SDK gRPC client/worker projects. Confirm the selected published NuGet graph and release maturity; upstream `main` is not a compatibility guarantee. Publish dependency minimums, not upper caps; validate minimum/current supported versions and all target frameworks.

### Capability matrix

These paths are not interchangeable. The host provides engine execution without virtual time; the context adapter provides method-level virtual time without an engine. The probes do not make either path a production UnitTestEx feature.

| Capability | In-memory SDK host | Method-level context adapter |
|---|---|---|
| Orchestration execution/replay | Real SDK engine; selected replay scenarios observed. No Functions worker. | Calls the real method directly; no engine history and `IsReplaying` is always false. |
| Activity and child calls | Registered delegates; selected activity/child paths executed. | Unsupported; calls fail explicitly. |
| Fan-out/fan-in | Selected real batch flow ran; general controlled completion ordering is not established. | Selected method test controlled C/B/A completions; no general scheduler or pump. |
| Timers/events | Host timers use wall-clock time; no virtual clock API. The wrapper warns and forwards. | `AdvanceBy` and manual event delivery for supported method-level cases; no durable timer history. Due timers fire before `AdvanceBy` returns, so this adapter does not model atomic same-deadline event/timer arbitration. |
| Serialization/failures | DTOs, nullable values, enums, nested collections, and selected failure-details paths tested; Functions compatibility remains unproven. | No boundary serialization; one failure-catch probe constructs an SDK exception directly. |
| Invalid awaits | Limited diagnostics; several non-durable await probes did not reach a terminal state under the watchdog. | No task-provenance or general invalid-await detector. |
| Lifetime/dependencies | Async host/client and in-process gRPC sidecar; explicit disposal; preview dependency graph. | Direct context use and disposal; no listener or host dependency. |
| Functions worker/backend | Does not prove Functions indexing, binding, or backend behavior. | Does not exercise the Functions worker or a backend. |

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

## Phase 1: decision-first feasibility spike

### Day 1: published in-memory host evidence

**Observed from the published artifact:** pin `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1** for the disposable probe. The NuGet feed lists five versions (0.1.0-preview.1 through 0.2.3-preview.1), all prerelease; there is no stable release. Its nuspec and package contain `net6.0`, `net8.0`, and `net10.0` assets. Nearest-framework selection would use net8 for net9 and net10 for net11; only net10 has been built and tested.

The published nuspec declares minimum dependencies `Microsoft.DurableTask.Client.Grpc`, `.Grpc`, and `.Worker.Grpc` 1.23.3; `Google.Protobuf` 3.33.5; `Grpc.AspNetCore.Server` 2.76.0 for net8/net10 (2.71.0 for net6); `Grpc.Net.Client` 2.76.0; and `Microsoft.Azure.DurableTask.Core` 3.7.1. These are the package's published requirements, not the resolved graph for this application.

**Source-supported conclusions, pinned to the nuspec repository commit `28d937706cb4ae7aacdf02de4198c26240637673` (not upstream `main`):**

- `DurableTaskTestHost.StartAsync` has public delegate registration for SDK function-style orchestrators and activities. The pinned SDK tests execute those delegates. `ProcessBatch` can be registered as a static delegate; `FinalizeBatch` can be called from a delegate on an instance. No attributes, private reflection, or Functions worker startup are needed for that SDK call path.
- The package creates its own Durable Task SDK worker host and an HTTP/2 loopback gRPC sidecar. It does not start the Azure Functions isolated worker or contact a Durable backend. This is an in-process SDK host, not a Functions runtime.
- Public `DurableTaskTestHostOptions` exposes `ConfigureServices`, `LoggerFactory`, and `Port`; `DurableTaskTestHost.Services` exposes its worker service provider. These can accept the application's registrations/instances and its `ILoggerFactory`. They do not automatically share the `FunctionTester` provider: a probe must explicitly bridge the needed application services.
- The host does not provide `FunctionContext`. UnitTestEx's public `TestFunctionContext` can be explicitly constructed with a service provider and passed by an activity delegate. That is an adapter at the probe boundary, not Functions worker behavior.
- There is no host clock injection or advance operation in the pinned public options/API. More importantly, the pinned `InMemoryOrchestrationService` compares scheduled messages with `DateTime.UtcNow` and enqueues future work through `Task.Delay(delay)`. A production one-hour or 24-hour durable timer therefore remains a real-time delay; a `TimeProvider` elsewhere would not establish virtual timer behavior. Do not run a production-length timer to reconfirm this.

Locally, `FunctionTesterBase` builds the application's entry point as-is and captures the host at `Build()` without starting its worker; `TestFunctionContext` accepts an `IServiceProvider`. Those existing hooks support a direct adapter without changing the app's `Program.cs`.

**Observed net10 dependency graph:** restore selected Durable Task abstractions/client/gRPC/worker gRPC **1.28.0**, `Microsoft.Azure.DurableTask.Core` **3.11.2**, `Google.Protobuf` **3.36.2**, `Grpc.AspNetCore.Server` **2.76.0**, and `Grpc.Net.Client` **2.84.0**. The Functions extension's higher minimums unified the host's lower 1.23.3/3.7.1/2.76.0 declarations without restore conflict. This net10 graph built and ran successfully; compatibility on net8/net9/net11 remains unverified. Keep the host dependency in the isolated net10 probe, not the multi-target production libraries. The published package has net8 and net10 assets rather than net9/net11-specific assets.

**Observed net10 runtime result:** the host probe suite passed all seven tests. The actual static `ProcessBatch` body and instance `FinalizeBatch` child completed through public function delegates; DTO input/output values and item ordering survived the SDK boundary. `ProcessBatch` observed `context.IsReplaying` on a later engine episode. The probe used the FunctionTester's service provider to construct scoped activities and supplied `TestFunctionContext` from each activity scope; the package extension reused the FunctionTester's logger factory, and the captured activity log assertion passed without changing application startup or starting the Functions worker. The actual `RecoverWorkflow` body caught the failed activity and returned `Declined`. The actual `DeclineWorkflow` body failed; returned orchestration metadata had root `TaskFailedException` details and nested `WorkflowRejectedException` type/message details. This is standalone SDK behavior, not Functions worker failure mapping.

**Observed net10 test:** [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs) also ran the real `ProcessBatch` body with a child delegate that failed during finalization. The parent completed as failed, and host metadata contained nested `TaskFailedException` and `WorkflowRejectedException` details. The child failure is deliberately injected at the SDK registration boundary; this does not establish Functions-worker failure mapping. A separate bounded test covers one SDK child-retry policy.

**Observed net10 tests:** the same host test gated the first activity, mutated the caller-owned input array after scheduling, and confirmed the orchestration output retained the original item; it also failed one item-activity attempt and observed the configured SDK retry succeed on the second attempt. [`ReplayDeterminismTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ReplayDeterminismTest.cs) changed the activity name on replay while registering both names and verified the host's nondeterminism diagnostic. These are SDK-host behaviors, not Functions-worker fidelity.

**Additional net10 tests:** `DurableTaskHostTest` also verified output-read isolation by mutating one deserialized `BatchResult` and confirming the next read was unchanged; an activity that fails every time stopped after the configured three attempts and left nested failure metadata. [`EarlyExternalEventTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/EarlyExternalEventTest.cs) raised a structured event while the real SDK orchestration was held at an activity, before its event wait existed, then verified buffered delivery and payload deserialization. [`PendingOrchestrationDisposalTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/PendingOrchestrationDisposalTest.cs) disposed the SDK host within five seconds while an orchestration remained pending on an external event.

**Additional net10 behavior:** [`ApprovalHostEventTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalHostEventTest.cs) also confirmed an explicit 100 ms input timeout completes with a denied decision and that a late event leaves a completed instance's output unchanged. [`SubOrchestrationRetryTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/SubOrchestrationRetryTest.cs) verified child retries recover on the third attempt and that exhaustion produces failure metadata with nested `WorkflowRejectedException` details. [`OrchestrationTerminationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/OrchestrationTerminationTest.cs) terminated a parent waiting on a child; the child completed or failed from its own external event, and the parent remained `Terminated`. [`ActivityTerminationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ActivityTerminationTest.cs) held an activity until after parent termination, then observed the activity delegate finish while the parent remained `Terminated`. The pinned `TaskActivityContext` API documentation lists `Name`, `InstanceId`, `Version`, and input access, but no cancellation-token property. `DurableTaskHostTest` reads the real batch orchestration's `Finalizing` status while its child is pending and after completion; [`CustomStatusProgressionTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/CustomStatusProgressionTest.cs) observed successive status values while running and the last value after completion. [`DurableSerializationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableSerializationTest.cs) round-tripped a nullable value, enum, and nested collection through standalone SDK orchestration/activity boundaries. The virtual-time suite now covers due/past deadlines, negative clock advances, non-UTC deadlines, multiple timers due in one advance, same-name wait selection, and the post-advance timeout/event boundary. Because `AdvanceBy` fires due timers before returning, exact same-deadline event/timer arbitration remains unmodeled.

**Published host serializer configuration inspection:** the pinned `Microsoft.DurableTask.InProcessTestHost` 0.2.3-preview.1 README and net10 API XML document `DurableTaskTestHostOptions` properties for `Port`, `LoggerFactory`, and `ConfigureServices`. They do not document a `DataConverter`, serializer, or serializer-options property; `ConfigureServices` is described as registration for application dependencies. Accordingly, the probe only establishes default SDK serialization, not custom serializer parity. Registering a converter service through the generic DI callback would not by itself prove the host consumes it, and no private override was attempted.

**Class-based SDK registration probe:** [`ClassBasedOrchestrationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ClassBasedOrchestrationTest.cs) uses the pinned host's public `DurableTaskRegistry.AddOrchestrator<TOrchestrator>()` registration path with a typed `TaskOrchestrator<string,string>`. On net10, the class-based orchestration called a registered activity and returned the result. This establishes only the standalone SDK class path; it does not establish Azure Functions worker integration, DI-based orchestrator construction, or a reusable UnitTestEx arrangement API.

**Still unproven:** this is standalone Durable Task SDK behavior, not Functions serialization/failure mapping; the host bypasses the Functions extension's input converters and worker. No timer completion or virtual-time runtime behavior was exercised because the pinned source has no clock control and future timers use real `Task.Delay`.

**Generated Functions dependency fix:** the sample's generated extension project now pins `Microsoft.AspNetCore.WebUtilities` **2.3.14**, the patched version for [GHSA-6527-9fp8-r4x6](https://github.com/advisories/GHSA-6527-9fp8-r4x6). The net10 dedicated suite passed 11/11 with no `NU1902`; the warning originated in the generated project, not the UnitTestEx package graph. Because Azure.Functions.Sdk 1.0.2 isolates that generated project from directory-wide targets, this fixture-scoped workaround injects the pin through its internal `_AzureFunctionPackageReference`/`WriteExtensionProject` seam. Remove it when the upstream dependency is patched or the SDK provides a supported generated-project override.

**Day-1 disposition and next branch:** the host invocation, serialization, explicit DI/logging/context bridge, and SDK failure-details probe pass on net10. The host may remain useful for real orchestration execution, but its pinned timer path cannot satisfy virtual-time scenarios. The test-only public context probe now shows that bounded virtual timers, ordered fan-in, and orchestration handling of SDK failure details are feasible as method-level tests. Keep the host as an optional real-engine path; do not add a timer wait to the host probe or silently shorten a production delay. No source evidence supports testing the Functions worker/runtime through this host.

### Day 2: public-context virtual-time feasibility

**Source-supported conclusion:** Durable Task abstractions 1.28.0 expose `TaskOrchestrationContext` as an abstract public type with overridable timer/event seams: `CurrentUtcDateTime`, `CreateTimer(DateTime, CancellationToken)`, and `WaitForExternalEvent<T>(string, CancellationToken)`. Its default `CreateTimer(TimeSpan, ...)` derives the deadline from `CurrentUtcDateTime`. This makes a minimal test-only context adapter technically possible without private APIs: control the clock, complete due timer tasks, and deliver/cancel event tasks while invoking the real `ApprovalOrchestration.AwaitApproval` method.

This proves only the production method's coordination against the provided context contract. It does not prove Durable Task engine replay, timer history/serialization, Functions extension behavior, or equivalence with the in-memory host. Unsupported context operations fail explicitly; this is not a general orchestration fake or a proposed production API.

**Experimental project and observed runtime result:** [`UnitTestEx.Azure.Functions.Durable`](../src/UnitTestEx.Azure.Functions.Durable/UnitTestEx.Azure.Functions.Durable.csproj) is a non-packable source-project prototype pinned to Durable Task abstractions 1.28.0; only its net10 target has been exercised. Its [`VirtualTimeOrchestrationContext`](../src/UnitTestEx.Azure.Functions.Durable/VirtualTimeOrchestrationContext.cs) supports virtual timers, external events, input, and cancellation observation; activity/child calls, custom status, `ContinueAsNew`, and `NewGuid` throw `NotSupportedException`. `IsReplaying` is always false. The dedicated [`UnitTestEx.Azure.Functions.Durable.Test`](../tests/UnitTestEx.Azure.Functions.Durable.Test/UnitTestEx.Azure.Functions.Durable.Test.csproj) project is in the solution, is non-packable, and its current suite has 17 method-level and 14 SDK-host tests. The real 24-hour approval body completed after a virtual 12-hour advance plus event delivery, with its timer canceled; advancing directly 24 virtual hours completed the timeout path and canceled the event wait. Additional tests observed that pre-canceled timer/event waits stay canceled (a later event is rejected), and an event delivered one tick before the timeout completes the approval path and cancels the timer. Multiple timers due within one advance all complete. A same-name event completes the most recent pending wait in this helper, leaving another wait pending for cancellation; this is not an engine contract. `AdvanceBy` fires due timers before it returns, so the adapter cannot test atomic same-deadline arbitration. The suite does not wait for production-duration timers.

**Observed hostless call/failure result:** [`BatchOrchestrationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/BatchOrchestrationTest.cs) runs the real static `ProcessBatch` body. Three `ProcessItem` tasks were completed in C/B/A order; the body remained pending until all completed, then passed A/B/C results to `TotalItems` and the finalization child. A second test supplied a public SDK `TaskFailedException` carrying `TaskFailureDetails.FromException(...)`; the real `RecoverWorkflow` catch filter recognized `WorkflowRejectedException` and returned `Declined`. The earlier five-test net10 probe suite passed in 107 ms. This failure test constructs the SDK exception directly; it does not demonstrate how a Durable Task engine or Functions worker maps an activity exception.

**Still unproven:** these contexts invoke method bodies directly and model only the calls needed by their tests. They do not model durable history/replay, engine scheduling, exact same-deadline timer/event arbitration, serialization, Functions failure mapping, or production failure details. The timer context can complete multiple timers due in one advance, but does not model engine event/timer ordering. The evidence supports a narrowly scoped test-only context adapter, not general orchestration fidelity or a production API.

### Invalid-await escape gate

**Observed on the pinned host, net10:** [`InvalidAwaitProbe`](../tests/UnitTestEx.DurableFunction.HostProbe/InvalidAwaitProbe.cs) ran four bounded cases. `Task.Delay` before a durable activity, `Task.Run` around a durable call, and `ConfigureAwait(false)` between durable calls each failed to reach a terminal state within the five-second watchdog; no engine diagnostic was returned before cancellation. The build emitted analyzer warnings `DURABLE0003` for `Task.Delay` and `DURABLE0004` for `Task.Run`; it emitted no warning for `ConfigureAwait(false)`. The direct-I/O case read `ReadA`, then an activity changed the file so replay attempted `ReadB`; the host failed it with `NonDeterministicOrchestrationException` and identified the activity-name mismatch at sequence 0. All four observations are asserted by the probe tests; the seven-test host probe suite passed.

**Conclusion and limit:** the host detects this induced history divergence, but the other cases show that these invalid-await patterns did not fail promptly with an actionable diagnostic in this setup. A watchdog timeout is not proof the orchestration could never complete. The test-only contexts above have no task-provenance or ambient-await guard, so they cannot claim to diagnose arbitrary `Task.Delay`, `Task.Run`, `ConfigureAwait(false)`, or direct I/O. Any initial context-backed surface must narrowly document supported awaits and fail explicitly for unsupported context operations; a general-purpose deterministic-pump claim is not supported by this evidence.

**Proposed budget:** five engineer-days for a decision, not a promise to implement nine gates across two engines. Phase 0 is separate. Owner: TBD.

1. **Day 1, host kill probes:** source review, package pin, and passing net10 runtime probe are recorded above. No supported host clock was found in the pinned public API or timer source.
2. **Days 2-3, selected branch:** bounded public-context probes cover virtual timers, ordered fan-in, and the orchestration's failure-details catch path. The invalid-await gate found one diagnosed replay divergence but three non-durable await cases without a terminal diagnostic under the watchdog. These probes do not validate Functions behavior or justify a general fake.
3. **Day 4, fixture comparison:** host evidence covers the real batch/child flow, replay, and both production failure-orchestration paths; the context probe covers the production approval body's timer/event coordination without wall-clock delay. These are complementary, not equivalent: the approval body's production-length timer was not exercised on the host, and the context has no engine replay. Exact same-deadline behavior, Functions-worker mapping, and broader cancellation/replay guarantees remain unproven. Fake self-replay remains optional research, not a parallel engine project.
4. **Day 5, decision:** use the real host for engine-backed orchestration tests that do not require virtual time, keeping its preview dependency isolated. A public-context adapter is a conditional, narrowly scoped option for method-level coordination and virtual-time tests; it must not claim engine replay, Functions behavior, or general deterministic-await detection. The context source-project prototype is non-packable; its runtime probes live under `tests`, so it is not a production API. Do not start a general fake. If the required contract demands production-duration timers with engine fidelity, host support remains blocked on a public clock seam such as [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843).

### Tiered evidence checklist

**Decision-critical:** failure prevents selection for that capability; complementary engines may cover different capabilities. Prefer fewer engines when one covers the required use cases.

| Gate | Concrete evidence |
|---|---|
| Invocation | Static and instance Azure Functions orchestration method bodies execute through public SDK delegate registration without app restructuring/private reflection/backend connection. Phase 0 supplies direct activity evidence. The SDK `TaskOrchestrator<TIn,TOut>` registration path is separately probed; this does not prove Functions-worker discovery or activation of `[Function]` methods. |
| Matching/fan-in | Three same-name calls matched by typed inputs complete C/B/A; pending after C/B; output retains task-list order. Unexpected and unused scripted calls fail diagnostically. |
| Serialization/failures | Mutating caller objects cannot alter boundary snapshots/results. DTO/enum settings are exercised. `TaskFailedException.FailureDetails.IsCausedBy<T>()`, type/message and nested causes match an executed SDK failure. Record Functions serializer differences. |
| Virtual time | For the pinned host, source shows duration-proportional real delay and no public clock control; it is ineligible for virtual-time scenarios without an upstream/public capability change. Do not await 1-hour/24-hour timers or shorten production delays. Apply the repeated warm-run comparison only to a candidate virtual-time engine; missing clock support does not disqualify the host for other tests. |

**Required follow-on, not engine-decision blockers:** a failed/unproven probe narrows the initial supported surface and creates an explicit implementation item. It does not discard passing decision-critical evidence. Only tested capabilities can be advertised; no universal fidelity claim is made for a narrower release.

| Area | Probe and consequence |
|---|---|
| Cancellation | Timer/event wait tokens, timeout overloads, pre-canceled tokens, cancellation of pending waits, losing-wait cleanup, bounded SDK-host disposal, parent termination with an independently completing or failing child, and an activity delegate finishing after parent termination are tested. The pinned activity-context API exposes no cancellation-token property. |
| Sub-orchestrations | Child success/failure, input/options propagation, retry recovery, retry exhaustion, and host failure metadata are tested. General nested-orchestration arrangements remain unproven. |
| Replay | The suite now forces host replay and uses a test-only counter to change the activity name, registering both names so failure is history divergence rather than missing registration. A bounded wait verifies `NonDeterministicOrchestrationException`. No public history injection or general invalid-await detector is assumed. |
| Invalid awaits | The pinned host diagnosed one replay divergence caused by changing direct-I/O output, but `Task.Delay`, `Task.Run`, and `ConfigureAwait(false)` cases produced no terminal state within five seconds. The build warned on the first two only. A timeout is not a complete detector; narrow context validity claims unless reliable provenance is demonstrated. |
| Dependencies/compatibility | Check published package graph, framework support and isolation. Restore/type incompatibilities block the affected package release, not all other fixture research. |

## Initial API proposal for review

**Evidence-backed initial test scope (net10 only; not a released contract):** keep the two prototype paths distinct. The SDK host can execute selected real static and instance Azure Functions orchestration method bodies, plus typed SDK `TaskOrchestrator<TIn,TOut>` implementations, with test-registered activity/child delegates, serialization boundaries, failure details, replay, events, custom status, and bounded host disposal. The fluent facade runs on this same SDK host, exposes completed/failed metadata through UnitTestEx-style assertions, and warns when registered function-based orchestration methods schedule real-time timers. The method-level context can exercise real orchestration-method timer/event coordination with virtual time, but has no durable engine, history, or replay. The dedicated suite now passes 53/53 on net10, including fluent output/failure assertions, custom-status progression, the full fan-out/fan-in test, test-only input-matched and explicitly invocation-ordered activity-response prototypes with delayed or manually released responses, and explicit activity invocation verification.

Do not extend those observations into a Functions-worker or binding claim, custom serializer parity, host virtual-clock support, exact same-deadline event/timer arbitration, or general activity/child cancellation. The host-disposal probe releases its blocked delegate after disposal; it demonstrates that disposal does not wait for or cancel that delegate, not worker shutdown semantics. The virtual-time helper's same-name waiter behavior is helper-specific and is not an SDK-host event-delivery contract.

Keep the two opt-in paths separate and thin. These are candidate public shapes, not approved or implemented package contracts.

| Path | Candidate API shape | Initial promise |
|---|---|---|
| SDK host (`UnitTestEx.Azure.Functions.Durable.TestHost`) | Make a UnitTestEx fluent builder on `FunctionTester` the primary entry point. The builder registers orchestrators and activity/child handlers, accepts input, starts the run, waits with a caller-supplied bound, and returns a result wrapper. It composes on the upstream `DurableTaskRegistry`, host client, and metadata APIs. Function-based registrations add a replay-safe wall-clock timer warning. | Real SDK engine execution, serialization boundaries, failure details, replay, and selected event/status/lifecycle behavior. Function-based methods and typed SDK `TaskOrchestrator<TIn,TOut>` are separate registration forms. The class-based SDK registration path remains available through the low-level `StartDurableTaskTestHostAsync` extension. |
| Virtual-time context (`UnitTestEx.Azure.Functions.Durable`) | Construct `VirtualTimeOrchestrationContext` with orchestration name/input; call the real method directly; control waits with `AdvanceBy` and `RaiseEvent`; await the method's returned task. | Method-level timer/event coordination against the helper's virtual clock, without durable engine history or replay. Activity and child calls remain explicitly unsupported. |

Illustrative fluent shape (names and exact types remain open):

```csharp
var run = await functions.DurableTask()
    .Orchestrator<BatchRequest, BatchResult>(
        nameof(BatchOrchestration.ProcessBatch),
        (context, _) => BatchOrchestration.ProcessBatch(context))
    .Activity<WorkItem, ItemResult>(
        nameof(WorkflowActivities.ProcessItem),
        (_, item) => InvokeRealActivityThroughFunctionTesterAsync(item))
    .StartAsync(request);

await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5), result => result.AssertCompleted().AssertOutput(expected));
```

For an orchestration with no input, use the separate `Orchestrator<TOutput>(name, Func<TaskOrchestrationContext, Task<TOutput>>)` overload and call `StartAsync()` without an argument. Do not represent absence of input with `null` or `default(T)`. The no-input registration uses the SDK's no-input delegate and client overload; it is not an alias for an explicit JSON-null input.

The SDK-host run implements `IExpectations<T>` so standard log expectations and custom expectations can inspect the returned `OrchestrationMetadata`. At assertion time, a non-completed orchestration is represented by `DurableTaskOrchestrationException`, which carries that metadata and its `FailureDetails`; this is an expectation value, not an exception thrown by the host. `ExpectSuccess()` therefore means the orchestration completed, while `ExpectException()` can assert a non-completed status. Host, scheduling, and wait errors still propagate directly. The result assertor remains the explicit API for `AssertCompleted()`, `AssertFailed()`, output checks, and nested failure-cause checks.

The run also exposes bounded `WaitForStartAsync`, `WaitForCompletionAsync`, and `WaitForCustomStatusAsync` methods, each defaulting to five seconds and accepting an explicit nullable timeout override, plus typed `RaiseEventAsync<TEvent>` for common event-driven tests. `WaitForCompletionAsync` accepts an optional assertion callback and still returns its assertor; the callback runs after arranged expectations are checked. Custom-status observation polls SDK metadata and returns the matching snapshot; the host retains responsibility for status serialization and persistence. Waiting for start is optional: the upstream SDK host buffers events raised before the orchestration registers its event wait.

The fluent API is the primary developer experience, but it must be a facade over the SDK host—not a second orchestration engine. Keep registration explicit and let activity handlers either invoke real methods through caller-owned FunctionTester scopes or provide deliberate test delegates. Activity invocation verification is opt-in through `run.Verify()` after completion; each activity registration accepts a `configure` callback with Moq `Times` expectations, defaulting to `AtLeastOnce()` when verification is requested. Retries count as handler invocations, and a registered but uncalled activity fails verification by default. This is not general strict matching, call-record snapshots, or simulated failure/replay semantics. Expose an advanced escape hatch to the upstream host/client for SDK features the fluent surface does not model; whether that is a builder property or a separate low-level entry point remains an API naming/design detail. Do not implicitly copy the FunctionTester service provider into the host.

The experimental TestHost framework now provides `ActivityResponse.Match<TInput,TOutput>` / `Next<TInput,TOutput>` and `ScriptedActivityHandler.ByMatch` / `InOrder`. Register `handler.InvokeAsync` with `.Activity(...)`, and dispose the handler using `using`. Its opt-in `Verify()` separately checks that every response plan was invoked, including plans still pending completion. Input matching rejects zero or multiple unused matching plans as activity failures; ordered responses are assigned by observed handler arrival, not guaranteed orchestration scheduling order. Each attempt consumes one plan, including failed attempts and SDK retries; exhaustion fails explicitly. Plans are frozen when attached to a handler and cannot be shared or reused. Response waiters receive output-factory failures, and disposing the handler cancels blocked release gates and delays without invoking their output factories. Cleanup does not undo an output factory already executing. The original full test and simplified scripted test both remain; focused tests exercise the framework implementation, including concurrent reservations in both matching modes. This is an experimental source API, not a release-ready contract.

Both scripted handler factories accept collection expressions containing response plans. `handler[index]` returns the plan at its zero-based declaration position, allowing indexed release and completion waits without separate response variables. For `ByMatch`, the index does not imply invocation order; input predicates still select plans. For `InOrder`, incoming calls consume plans in declaration order.

For the context helper, initially expose only the clock/event controls and the state needed to test those controls. Document that same-deadline timer/event arbitration is not modeled and that same-name event selection is helper-specific. Do not present this as SDK-host behavior or as a general orchestration fake.

Neither path starts the Functions worker or proves function discovery, trigger binding, Functions serializer/failure mapping, or backend behavior. Host timers remain real wall-clock timers; the host has no virtual clock. Cancellation claims remain limited to the specific wait, termination, and disposal probes. Keep `UnitTestEx.Azure.Functions` dependency-free of Durable Task and do not change the existing `Trigger<T>()` behavior.

**Host context decoration and timer diagnostics (requirement):** the host-testing adapter may decorate the host-supplied `TaskOrchestrationContext` for logging and diagnostics. Its default behavior must transparently forward calls, including `CreateTimer`, preserving the host's engine behavior. When the pinned host handles a timer without virtual-clock support, emit an actionable test diagnostic that the timer uses real wall-clock time and may wait for its requested duration; avoid repeating the same diagnostic on every replay. The warning does not prevent a long wait. Any future timer override must be explicit and must not imply engine timer/replay fidelity unless it participates in the host scheduler. Do not silently shorten production timers.

**Observed net10 probe:** [`HostTimerDiagnosticTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/HostTimerDiagnosticTest.cs) wrapped the host-provided context, forwarded a test-only 100 ms timer, and completed the orchestration through a subsequent activity. The host replayed the orchestration; exactly one warning was captured, and elapsed time exceeded 50 ms, showing the timer was not replaced by an immediately completed task. This checks transparent forwarding and replay-safe diagnostics for this short probe only; it does not provide virtual time or establish production-duration timer behavior.

**Observed net10 test:** [`ApprovalHostEventTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalHostEventTest.cs) scheduled the real approval orchestration with its 24-hour input timeout, raised the `Approved` event after the instance started, and observed completion within a bounded five-second wait. The host emitted the real-wall-clock timer diagnostic; this confirms the event can win without waiting for or shortening the production-length timer, but does not establish same-deadline arbitration.

**Compatibility:** generic `Trigger<T>()` currently accepts orchestration/entity attributes and can be used with caller mocks. Preserve that behavior now. Rejection is not committed; if proposed later, it requires a major version of the existing Functions package, release notes, and an error linking to optional-package guidance even when not installed. Entities remain outside the new tester's scope.

## Later phases and Aspire

- **Phase 2, minimal opt-in implementation:** prototype the two selected packages separately: the method-level timer/event context helper and the optional in-memory SDK host wrapper. Keep the existing `UnitTestEx.Azure.Functions` package dependency-free of Durable Task. The context path must stay narrow; the host path must preserve real engine timers/replay and warn about wall-clock waits. Do not build the broad scripted fake, a reusable UnitTestEx adapter for `TaskOrchestrator<TIn,TOut>`, retry harness, or Functions backend integration as part of this phase.
- **Phase 3, expanded interactions:** add child orchestration, cancellation edge cases, replay and diagnostic capabilities as their evidence matures; maintain the capability matrix and explicit unsupported-operation failures.
- **Phase 4, release:** public docs/assertions, dependency/framework matrix, and package isolation. README documentation must include a detailed support table organized by Durable Functions type/construct (at minimum activities, static/instance `[Function]` orchestration methods, typed SDK `TaskOrchestrator<TIn,TOut>` implementations, sub-orchestrations, entities, timers, external events, and client/start/status operations). Identify SDK-only behavior separately from Functions-worker behavior. For each, distinguish the test path, supported behavior, unsupported or unproven behavior, and fidelity boundary; distinguish direct `FunctionTester` invocation, the in-memory SDK host, method-level virtual-context tests, and Aspire/real Functions integration. Do not advertise unproven behavior as supported. The interim evidence matrix is [`azure-durable-functions-support-matrix.md`](./azure-durable-functions-support-matrix.md); it is not a substitute for the release README. Standalone worker/backend integration, entities, `ContinueAsNew`, and durable HTTP are not initial claims.

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
- [Published 0.2.3-preview.1 NuGet page](https://www.nuget.org/packages/Microsoft.DurableTask.InProcessTestHost/0.2.3-preview.1), [published nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.durabletask.inprocesstesthost/0.2.3-preview.1/microsoft.durabletask.inprocesstesthost.nuspec), pinned [host API](https://github.com/microsoft/durabletask-dotnet/blob/28d937706cb4ae7aacdf02de4198c26240637673/src/InProcessTestHost/DurableTaskTestHost.cs), and pinned [timer service](https://github.com/microsoft/durabletask-dotnet/blob/28d937706cb4ae7aacdf02de4198c26240637673/src/InProcessTestHost/Sidecar/InMemoryOrchestrationService.cs): release maturity, target groups/dependencies, host API, and wall-clock timer implementation. This replaces upstream `main` as the compatibility reference.
- [Published Functions Durable extension 1.22.0 nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.azure.functions.worker.extensions.durabletask/1.22.0/microsoft.azure.functions.worker.extensions.durabletask.nuspec): comparison point for the existing sample's Durable SDK dependency minimums.
- Local [`FunctionTesterBase`](../src/UnitTestEx.Azure.Functions/Azure/Functions/FunctionTesterBase.cs) and [`TestFunctionContext`](../src/UnitTestEx.Azure.Functions/Azure/Functions/TestFunctionContext.cs): startup capture and optional context construction used by the staged probe.
- [SDK context wrapper](https://github.com/microsoft/durabletask-dotnet/blob/28d937706cb4ae7aacdf02de4198c26240637673/src/Worker/Core/Shims/TaskOrchestrationContextWrapper.cs), [Functions wrapper](https://github.com/Azure/azure-functions-durable-extension/blob/58159da937ad7703d1fadb8ea7112d4e9073ba43/src/Worker.Extensions.DurableTask/FunctionsOrchestrationContext.cs), and [input converters](https://github.com/Azure/azure-functions-durable-extension/blob/58159da937ad7703d1fadb8ea7112d4e9073ba43/src/Worker.Extensions.DurableTask/FunctionsOrchestrationContext.InputConverter.cs): different `GetInput<T>()` paths; the Functions wrapper is pinned to extension 1.22.0's nuspec commit.
- [`TaskFailedException`](https://github.com/microsoft/durabletask-dotnet/blob/28d937706cb4ae7aacdf02de4198c26240637673/src/Abstractions/TaskFailedException.cs) and [`TaskFailureDetails`](https://github.com/microsoft/durabletask-dotnet/blob/28d937706cb4ae7aacdf02de4198c26240637673/src/Abstractions/TaskFailureDetails.cs): public construction and mapping; validate pinned-package behavior.
- [Durable analyzer](https://learn.microsoft.com/en-us/azure/azure-functions/durable-functions/durable-functions-roslyn-analyzer), [`host.json` settings](https://learn.microsoft.com/en-us/azure/azure-functions/durable-functions/durable-functions-host-json-settings), and [scheduler quickstart](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/quickstart-durable-task-scheduler).
- Existing UnitTestEx [Functions](./azure-functions.md) and [Aspire](./aspire.md) documentation define the invocation/runtime proof boundaries.
