---
name: unittestex-durable
description: Write UnitTestEx Durable Functions preview orchestration tests using the in-memory SDK host, fluent registration, ActivityMock, invocation verification and managed diagnostics. Use to distinguish real orchestration replay from method-level virtual time or real Functions worker/backend integration.
license: MIT
---

# Durable Functions testing (v7 preview)

Use `UnitTestEx.Azure.Functions.Durable` **7.0.0-preview** with coordinated regular **7.0.0** UnitTestEx dependencies. It depends on the Functions package and core; use their skills for startup, output and ordinary DI conventions.

This is an evolving implementation targeting key scenarios, not exhaustive Durable Functions coverage. It pins Microsoft's preview `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1**; APIs may break between preview releases. Check the support matrix for the installed version.

## Select the execution model before writing a test

| Claim | Model |
|---|---|
| Activity-method logic, DI, logging, original exceptions | Ordinary `FunctionTester.Trigger<T>()`; the Durable package is not required. |
| Orchestration serialization, scheduling, replay, activities/children and failure metadata | Fluent in-memory Durable Task SDK host. |
| Timer/event branch logic with instant advancement | `VirtualTimeOrchestrationContext`, a separate narrow method-level context. |
| Functions indexing/bindings, real worker and backend behavior | Aspire/Core Tools plus the backend/emulator. |

Do not swap models to make an unsupported scenario appear covered.

## SDK-host setup

Preserve normal application startup. `FunctionTester.Create<T>()` uses an ordinary class in the Functions application assembly. For a direct orchestration test select the root explicitly with `.Orchestrator(...)`; opt into `DurableTask().WithSelfDiscovery()` before it to discover supported activities and children. For starter tests use the session recipe below. SDK-host types/extensions use `UnitTestEx.Azure.Functions.Durable.TestHost`; virtual-time context uses `UnitTestEx.Azure.Functions.Durable`.

The minimal registration shape is:

```csharp
using System;
using System.Threading.Tasks;
using UnitTestEx;
using UnitTestEx.Azure.Functions.Durable.TestHost;

using var functions = FunctionTester.Create<WorkflowActivities>();
await using var run = await functions.DurableTask()
    .Orchestrator<string, string>("OutputProbe", (_, input) => Task.FromResult(input))
    .StartAsync("hello");
await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10),
    result => result.AssertCompleted().AssertOutput("hello"));
```

`WorkflowActivities` is an application marker (the repository sample uses that name). This tiny delegate only checks host wiring; replace it with a call to the application's real orchestration to test production behavior. The compiled `FluentDurableTaskTest.WritesManagedOutputBeforeAssertionsOnce` exercises this registration/completion shape.

Use `await using` for runs and `using` for the owning Functions tester and activity mocks. Caller-created typed orchestrator instances and their DI scopes remain caller-owned; keep them alive through replay/continuation.

`WithSelfDiscovery()` scans only the parent FunctionTester's marker assembly; `WithSelfDiscovery<T>()` adds the specified marker's assembly. No referenced-assembly scanning occurs. Public `[Function]`/`[ActivityTrigger]` methods execute real bodies with per-invocation application DI scopes; explicit `.Activity(...)` handlers override discovered methods. Discovery supports one trigger input plus `FunctionContext`/`CancellationToken`, synchronous/void/Task/ValueTask results. Unsupported signatures and duplicate discovered names fail early. Discovered handlers have no implicit call-count expectations. Use explicit registration for verification or unsupported binding requirements.

Public `[Function]` orchestration methods with `[OrchestrationTrigger] TaskOrchestrationContext` and `Task`/`Task<T>` return are also discovered. Explicit registrations override discovery. Discovered instance orchestrations use separate application DI scopes per instance, retained across replay/continuation until the owning run or session is disposed. Context input is converted through the configured SDK worker converter; custom converters must support object-to-typed conversion. Replay still demands deterministic code and replay-safe logging.

Discovery is opt-in because real activities can have external side effects. It does not execute worker middleware or binding conversion. Supplied cancellation tokens are non-cancelable; parent termination is not activity cancellation. The compiled showcase compares explicit handlers, discovery with an explicit child override, and `BatchFanOutFanInWithDiscoveredSubOrchestrator`, which runs the real child and its journal activities while mocking item processing.

For a starter method, use `await using var session = await functions.DurableTask().WithSelfDiscovery().StartHostAsync();`, pass `session.Client` to the method via the appropriate trigger tester, then `await using var run = session.Observe<TOutput>(instanceId);` and use `run.WaitForCompletionAsync(...)`. The ID must be known from the application's input, response or another application mechanism. The run borrows the session; dispose it before the session and FunctionTester. `.ConfigureRegistry(...)` supplies explicit SDK handler overrides before discovery, without fluent activity-count expectations. This tests direct starter invocation and SDK execution, not real Service Bus delivery or Functions `[DurableClient]` binding. A mocked client can independently verify scheduling arguments without executing the workflow. See `DurableStarterTest` for both paths.

## Activities and assertions

Use `ActivityMock.ByMatch` when concurrent dispatch order is nondeterministic. `ActivityResponse.Match<TIn,TOut>` binds plans by input; `.Pending()` holds a response until the test releases it. `.After(...)` is a real-time handler delay, not virtual engine time.

Wait for invocations before releasing held responses. Bound waits and releases with timeouts. Handler completion order is not engine result-processing order. Mocked activities/children do not execute their real production bodies: test those separately when needed.

Apply fluent expectations before completion. Assert orchestration status/output after `WaitForCompletionAsync`. Call `mock.Verify()` for plan consumption and `run.Verify()` for configured invocation counts; retries are additional invocations. Managed output includes orchestration identity, inputs, logs and failure/timeout metadata; inspect it rather than swallowing failures.

## Hard boundaries

- SDK-host timers and retry delays use wall-clock time. No supported host virtual-clock API exists in the pinned host; do not invent one.
- `VirtualTimeOrchestrationContext` has no engine history/replay and rejects activities/children. It does not advance the SDK host.
- The pinned host cannot preserve buffered events correctly across `ContinueAsNew` (upstream issue #844). Preservation requests warn; do not emulate success by reinjecting events. Use the real-worker tier to test preservation.
- SDK-host success is not proof of Functions serializer/binding parity, worker delivery or deployed Azure behavior.

Run the consumer's focused test. Feature gaps should be reported with the scenario, not hidden behind test-only application rewrites or an emulated engine.

- [Guide, showcase and support matrix](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md)
- [Compiled batch/activity-mock showcase](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs)
- [Compiled SDK-host examples](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs)
- [Request additional scenarios](https://github.com/Avanade/UnitTestEx/issues/new/choose)
