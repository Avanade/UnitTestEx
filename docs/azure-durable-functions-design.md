# Azure Durable Functions testing design

[Back to README](../README.md) | [User guide and showcase](./azure-durable-functions.md) | [Support matrix](./azure-durable-functions.md#support-matrix)

## Architecture

Durable testing has distinct execution paths. They share application fixtures and UnitTestEx conventions, but not an orchestration engine.

| Path | Responsibility | Boundary |
|---|---|---|
| Direct activity invocation | Existing `FunctionTester.Trigger<T>()` invokes activity methods with application DI, supplied context, captured logs and original exceptions. | No Durable scheduling, history, replay or failure translation. |
| In-memory SDK host | `FunctionTester.DurableTask()` registers real orchestration bodies and activity/child delegates, runs the Durable Task SDK engine and exposes metadata, assertions and diagnostics. | No Functions worker, binding pipeline or external Durable storage. Timers use wall-clock time. |
| Method-level virtual-time context | `VirtualTimeOrchestrationContext` invokes a real method against controlled timer/event waits. | No engine, history, replay or serialization boundary. Activity and child calls are unsupported. |
| Real Functions integration | A separate Aspire AppHost runs Core Tools, the normal isolated worker and Durable Functions extension against Azurite's Azure Storage endpoints. | Requires a container runtime. Local emulator behavior is distinct from deployed Azure authentication, scale and reliability. |

The fluent SDK-host facade is the primary orchestration-testing API. The method-level context is a narrow alternative for timer/event coordination, not a replacement Durable engine.

## Package and framework boundaries

`UnitTestEx.Azure.Functions.Durable` is one optional preview package containing the SDK-host facade, activity mocks and virtual-time context. The host types use the `UnitTestEx.Azure.Functions.Durable.TestHost` namespace; the context uses `UnitTestEx.Azure.Functions.Durable`.

The package references the existing `UnitTestEx.Azure.Functions` package in one direction. Ordinary Functions testing does not acquire Durable dependencies. Both Durable testing modes share the optional package's dependency graph.

The project pins Microsoft's `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1** and `Microsoft.DurableTask.Abstractions` **1.28.0**. The integration remains preview because the upstream host is preview; breaking changes may occur between releases.

Package versioning sets `PackageVersion` to `$(Version)-preview` after importing `Common.targets`. Numeric assembly versions and UnitTestEx dependency references retain the regular shared version. All packages release as a coordinated set: base `6.2.0` produces Durable `6.2.0-preview`, and base `6.2.1` produces `6.2.1-preview`.

Libraries target .NET 8/9/10/11. Core suites target all four frameworks; Aspire and dedicated Durable suites target .NET 10/11. Fixed AppHosts/resources and the real Durable Functions integration project target .NET 10.

## Normal application startup

The sample uses ordinary top-level [startup](../tests/UnitTestEx.DurableFunction/Program.cs), registering `IWorkflowJournal` through application DI. Testing does not require a custom `Program` class, `BuildHost` method or test-only startup structure.

`FunctionTester.Create<WorkflowActivities>()` identifies the application assembly. The SDK-host builder explicitly registers delegates; it does not discover `[Function]` methods or bind trigger arguments. Delegates can invoke static methods, resolve instances through application scopes, supply `TestFunctionContext`, or substitute behavior.

Application scopes and contexts remain caller-owned. `.ConfigureHostServices(...)` configures the separate SDK host, not the captured application's service collection.

## SDK-host facade

`FunctionTester.DurableTask()` composes on the upstream registry, client and metadata APIs. It supplies:

- Explicit function-based orchestration/activity/child registration and typed root/child instances.
- Separate input and no-input scheduling overloads.
- Bounded start, completion and custom-status waits, defaulting to five seconds.
- Typed external events and output, status and nested-failure assertions.
- UnitTestEx expectations, captured logs and completion callbacks.
- Bookended completion/timeout diagnostics and asynchronous disposal.
- SDK-client access through `run.Host.Client`.

Typed SDK `TaskOrchestrator<TInput,TOutput>` instances register through `.Orchestrator(name, instance)` and `.SubOrchestrator(name, instance)`. These overloads route `RunAsync` through the same decorated SDK context as function delegates, preserving managed diagnostics, typed scheduling and failure assertions. They do not discover classes or construct them automatically.

The caller constructs typed instances, optionally using `ActivatorUtilities` and an application scope, and owns their dependencies until the run is disposed. The instance is reused during replay and continuation; durable state belongs in input/history rather than mutable instance fields. The low-level registry callback remains available for SDK factory/type registration and SDK-host DI.

`WaitForCompletionAsync` returns an assertor for terminal metadata; completion of the wait does not mean orchestration success. SDK failure details remain metadata rather than rethrown original activity exceptions. Expectations use `DurableTaskOrchestrationException` to represent a non-completed orchestration, retaining its metadata and nested causes.

Diagnostics run before expectations and assertion callbacks. Terminal output appears once per run. A completion timeout reports the last available state without marking the instance failed or terminating it. Metadata retrieval has an additional 250 ms bound; retrieval failures do not replace the original timeout. Retained log snapshots keep timeout diagnostics from draining messages needed by later expectations.

SDK lifecycle output remains separate. Do not assume a total chronology across resource logs, engine messages and application logs.

## Activity responses and verification

`ActivityMock.ByMatch` selects exactly one unused matching response plan. Zero matches, ambiguous matches and exhaustion fail explicitly; there is no fallback.

`ActivityMock.InOrder` consumes response plans by handler arrival. Use it for successive outcomes with identical input, including retries. Arrival order is not orchestration source order.

`ActivityResponse.Match` and `Next` provide typed output factories. Responses can complete immediately, after a real delay with `.After(...)`, or after explicit release with `.Pending()`. Each attempt consumes a plan, including factory failures and retries. Plans freeze on attachment and cannot be shared or reused.

Indexed access follows zero-based declaration order. Response completion identifies handler completion, not engine result-processing order. `Task.WhenAll` preserves its input task order independently of activity completion order.

Verification is explicit: `mock.Verify()` checks plan consumption; `run.Verify()` checks registered activity counts. Neither runs automatically. Retries count as additional invocations.

Disposing a mock cancels blocked gates, delays and response waiters; it cannot stop a synchronous output factory already executing. SDK-host disposal likewise does not cancel caller-owned blocked activity or child work.

## Continuation and custom retries

`ContinueAsNew(newInput, preserveUnprocessedEvents)` is forwarded to the SDK engine. Each generation receives serialized input and a fresh history under the same instance ID. Orchestrator code must return immediately after the call; its interim return value is not the final workflow output.

**Buffered-event preservation is unsupported by the pinned in-memory host.** Its continuation dispatcher discards self-events used by the SDK to carry buffered payloads into the next generation; see [microsoft/durabletask-dotnet#844](https://github.com/microsoft/durabletask-dotnet/issues/844). Use `preserveUnprocessedEvents: false` for discard semantics. A preservation request emits a replay-safe warning and is forwarded unchanged, not simulated or repaired by reinjecting events. Use the real Functions backend for preservation behavior.

A fluent run spans these generations: completion assertions use the terminal generation's output and metadata, including its input, while activity verification and mock-plan consumption cover the whole run. Long-running continuations still require bounded waits and explicit completion/termination conditions. The method-level virtual context does not model continuation.

Activity and child calls can supply `TaskOptions.FromRetryHandler(...)` with synchronous or asynchronous SDK handlers. Decisions use `RetryContext.LastAttemptNumber` and `LastFailure`; child failures can require walking nested details to identify the original cause. Each retry consumes another response and counts as another activity invocation.

Retry handlers are orchestration code and must remain deterministic. Asynchronous handlers may await durable tasks through the orchestration context, not arbitrary I/O or `Task.Delay`. Handler timer waits use the host's wall clock. UnitTestEx does not implement a second retry engine or shorten delays.

## Timers, replay and cancellation

The SDK host has **no supported virtual clock**. Engine timers and retry delays use wall-clock time. The facade decorates the host-supplied context, forwards durable calls unchanged and emits a replay-safe timer warning. Clock control is requested in [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843).

Injecting `TimeProvider` or delaying a mock response does not advance engine timers. Never silently shorten production delays. A shorter duration supplied as explicit orchestration input tests that value; an event winning against a 24-hour timer tests the event path, not eventual timer completion.

`VirtualTimeOrchestrationContext.AdvanceBy(...)` advances method-level timer waits without sleeping. Due timers fire before the call returns; atomic same-deadline event/timer arbitration is not modeled. The helper completes the most recent same-name event wait and exposes no engine event-delivery contract. `IsReplaying` is always false.

Use replay-safe context time, IDs and logging inside orchestrations. The SDK host detects divergent durable history, but is not a universal detector of invalid awaits or arbitrary I/O. Follow the upstream orchestration constraints and use the Durable Task analyzer.

The virtual context supports timer/event wait cancellation. The pinned SDK activity context exposes no cancellation-token property. Parent termination does not imply activity/child cancellation; callers must clean up their own blocking work.

## Real Functions worker/backend integration

[`UnitTestEx.Aspire.Durable.AppHost`](../tests/UnitTestEx.Aspire.Durable.AppHost) hosts the unchanged sample through Core Tools and its .NET 10 isolated worker. A per-test nonpersistent Azurite 3.37.0 container supplies blob, queue and table storage with API-version validation enabled; a random task hub isolates Durable state.

[`UnitTestEx.Azure.Functions.Durable.Integration.Test`](../tests/UnitTestEx.Azure.Functions.Durable.Integration.Test) exercises:

- The batch orchestration, real item activities, finalization child and injected journal, including input/output, custom status and worker logs.
- Activity-failure recovery through the Functions extension boundary.
- Approval-event delivery while retaining the application's 24-hour timer.
- Continuation with preservation/discard of buffered events through the real extension and storage provider.

The tests verify indexed functions with `WaitForFunctionsAsync`, schedule through the extension's HTTP management API and poll status with bounded waits. Management URLs are restricted to the test host. Completed parent history is purged; AppHost disposal stops the worker and removes the nonpersistent container, including child history and timer data.

Test configuration reduces storage queue polling to one second, not application timers. Ports 7073 and 11000-11002 require sequential execution on a shared host. The [CI workflow](../.github/workflows/ci.yml) runs the separate integration job automatically on PRs; manual dispatch enables it through `durable-integration: true`.

This integration is separate from the container-free SDK-host/context suite and does not add emulator dependencies to the published Durable package. The sample journal only logs; it is not a publishing-service integration.

## Scope limits

The Durable tester does not provide a general scripted orchestration engine, deterministic pump or self-replay fake. Engine scheduling, history, continuation and retries remain Microsoft's responsibility. The method-level virtual context intentionally excludes these engine behaviors.

Automatic function/class discovery, a higher-level entity-testing API and built-in SDK-host durable HTTP are outside the supported surface. Typed root/child registration is explicit and caller-owned; SDK type/factory registration remains accessible through the registry callback.

Durable HTTP and entity execution belong to a Functions-runtime integration when extension behavior or state/locking semantics matter. A substituted activity response only tests the orchestration's branch against that response; it does not reproduce the extension's HTTP machinery or entity dispatcher.

The in-memory host does not execute Functions middleware, binding converters, starter triggers or production HTTP status endpoints. Use the real Functions integration path for worker/backend interactions. Deployed Azure configuration, authentication and reliability require a separate deployment-level test.

Preserve existing generic `Trigger<T>()` behavior, including caller-supplied orchestration mocks. Adding the optional Durable package does not change direct trigger invocation.
