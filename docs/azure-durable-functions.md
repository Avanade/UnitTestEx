# Azure Durable Functions testing

**Preview disclaimer:** This package is intentionally marked as preview because it depends on Microsoft's preview `Microsoft.DurableTask.InProcessTestHost` package. Neither the dependency nor this integration should be treated as a stable API contract: **breaking changes may occur at any time**, including between preview releases.

The optional **`UnitTestEx.Azure.Functions.Durable`** preview package includes the in-memory SDK-host facade, activity mocks and the separate method-level virtual-time context. Its package version is `$(Version)-preview`, using the shared `Common.targets` version: currently **`6.2.0-preview`**. Use regular **`6.2.0`** versions of the other UnitTestEx packages, including your test-framework package. All packages release as a coordinated set; a shared version bump to `6.2.1` produces Durable `6.2.1-preview`, not an independently numbered preview.

**Clock limitation:** The SDK host has no supported virtual-clock control; orchestration timers and retry delays use wall-clock time. Our upstream request is tracked in [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843). The separate method-level virtual-time context does not change the SDK host's clock; see [timers and virtual time](#timers-and-virtual-time).

Add the package alongside your chosen UnitTestEx test-framework integration:

```xml
<PackageReference Include="UnitTestEx.Azure.Functions.Durable" Version="6.2.0-preview" />
```

The host API retains the `UnitTestEx.Azure.Functions.Durable.TestHost` namespace; this is not a separate package. `VirtualTimeOrchestrationContext` remains in `UnitTestEx.Azure.Functions.Durable`. Runtime evidence is **.NET 10 only**.

[Back to README](../README.md) | [Support matrix](#support-matrix) | [Design and implementation status](./azure-durable-functions-design.md)

## Choose the right test

| Question | Test path | Boundary |
|---|---|---|
| Does an activity method use its services and return the right value? | Existing `FunctionTester.Trigger<T>()`; see [activity invocation](./azure-functions.md#durable-activity-invocation). | Direct invocation, not Durable scheduling or failure translation. |
| Does the real orchestration coordinate activities, children, events and failures? | Preview `FunctionTester.DurableTask()` in the optional Durable package. | The standalone in-memory Durable Task SDK engine, including its serialization and replay; not the Functions worker. |
| Does timer/event coordination work when advancing a long timeout instantly? | `VirtualTimeOrchestrationContext` in the optional Durable package. | Narrow method-level context, not engine scheduling, history or replay. It rejects activity and child calls. |
| Can Functions discover, bind and run the workflow against a backend? | A real Functions host with backing services, for example a separate opt-in Aspire/Core Tools test. | Not demonstrated by the current Durable suite. |

The existing Functions package remains independent of Durable Task. The optional Durable package references it, not the other way around. Both testing modes share this one package and its host dependencies; they remain distinct execution models. The host pins `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1**; its framework/dependency implications are recorded [below](#dependency-and-framework-boundary). Source targets include .NET 8/9/10/11, but only .NET 10 has been exercised.

## Showcase: fan-out, controlled completion, fan-in

The runnable [showcase test](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs) executes the real [`BatchOrchestration.ProcessBatch`](../tests/UnitTestEx.DurableFunction/BatchOrchestration.cs). It normalizes the reference, fans out item calls, totals their results, and calls a child orchestration. The test substitutes item processing and finalization, not the parent orchestration.

### Understand the application being tested

The [input/output records](../tests/UnitTestEx.DurableFunction/WorkflowModels.cs) describe a `BatchRequest` containing a reference and `WorkItem[]`, each with an ID and amount. Each processed `ItemResult` retains the ID and doubles the amount. The resulting `BatchResult` contains the normalized reference, summed processed amounts and ordered results.

| Function/body | Role in the application | What the showcase executes |
|---|---|---|
| [`BatchOrchestration.ProcessBatch`](../tests/UnitTestEx.DurableFunction/BatchOrchestration.cs) | Static parent orchestration: read input, normalize the reference, fan out item calls, fan in their results, calculate the total, report `Finalizing`, then await a finalization child. | **Real body**, using the SDK host's orchestration context. |
| [`ActivityTransforms.NormalizeReference`](../tests/UnitTestEx.DurableFunction/ActivityTransforms.cs) | Reject blank input, trim whitespace and convert the reference to invariant uppercase. | **Real body**, once: `" batch-7 "` becomes `"BATCH-7"`. |
| [`WorkflowActivities.ProcessItem`](../tests/UnitTestEx.DurableFunction/WorkflowActivities.cs) | Reject negative amounts, log the item ID through `FunctionContext` and asynchronously return the ID with twice the amount. | **Mocked**, three calls: outputs A/20, B/40 and C/60. Its production validation, activity logging and context are not exercised here. |
| [`ActivityTransforms.TotalItems`](../tests/UnitTestEx.DurableFunction/ActivityTransforms.cs) | Sum processed `ItemResult.Amount` values, not original input amounts. | **Real body**, once: 20 + 40 + 60 = 120. |
| [`BatchOrchestration.FinalizeBatch`](../tests/UnitTestEx.DurableFunction/BatchOrchestration.cs) | Instance child orchestration: call `RecordWorkflow`, then `PublishWorkflow`, then return the aggregate unchanged. | **Substituted child delegate**, which immediately returns its input. The parent still makes a real SDK sub-orchestration call, but this production child body is not executed. |
| [`WorkflowActivities.RecordWorkflow`](../tests/UnitTestEx.DurableFunction/WorkflowActivities.cs) | Send the normalized reference to the injected `IWorkflowJournal`. | **Not invoked**, because finalization is substituted. |
| [`WorkflowActivities.PublishWorkflow`](../tests/UnitTestEx.DurableFunction/WorkflowActivities.cs) | Send the completed aggregate to the injected journal and await publication. | **Not invoked**. The [sample journal](../tests/UnitTestEx.DurableFunction/WorkflowJournal.cs) only logs; it is not a storage or broker integration. |

`ProcessBatch` materializes all three activity calls before awaiting `Task.WhenAll`, allowing concurrent execution. `Task.WhenAll` returns results in the original task-list order: the test finishes handlers C/B/A, but expects A/B/C in the aggregate. Each item call has a maximum of **three attempts** with a **one-second initial retry interval**; this showcase supplies successful responses and does not exercise retry failure/recovery.

Before scheduling the child, the parent writes a `Finalizing` custom status with the durable context's UTC time and emits a replay-safe log message. It derives the child instance ID from `context.NewGuid()`, not a nondeterministic `Guid.NewGuid()`. Its final return value comes from the child; the showcase's pass-through child makes that the aggregate unchanged.

`FunctionTester.Create<WorkflowActivities>()` uses that type to find the application's normal [entry point](../tests/UnitTestEx.DurableFunction/Program.cs), which registers `IWorkflowJournal`. It does not automatically invoke or register all of the class's activities. The builder below explicitly chooses each real or substitute delegate. Real `ProcessItem` validation/context/logging and journal behavior have separate [direct activity tests](../tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs); the full SDK-host workflow is exercised in [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs).

### Arrange and run the showcase

The types below come from the [sample application](../tests/UnitTestEx.DurableFunction). Imports are `UnitTestEx`, `UnitTestEx.Azure.Functions`, `UnitTestEx.Azure.Functions.Durable.TestHost`, `UnitTestEx.DurableFunction`, `Moq`, and `NUnit.Framework`; log expectations additionally use `UnitTestEx.Expectations`.

```csharp
// Match by input rather than nondeterministic concurrent dispatch order.
using var processItems = ActivityMock.ByMatch(
[
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "A", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "B", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "C", item => new ItemResult(item.Id, item.Amount * 2m)).After(TimeSpan.FromMilliseconds(100))
]);

// Normal application startup; no custom Program class or running Functions worker.
using var functions = FunctionTester.Create<WorkflowActivities>();
var request = new BatchRequest(" batch-7 ", [new("A", 10m), new("B", 20m), new("C", 30m)]);

await using var test = await functions.DurableTask()
    .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
    .Activity<string, string>(nameof(ActivityTransforms.NormalizeReference), (_, reference) => ActivityTransforms.NormalizeReference(reference), configure: options => options.Times(Times.Once()))
    .Activity<WorkItem, ItemResult>(nameof(WorkflowActivities.ProcessItem), processItems.InvokeAsync, configure: options => options.Times(Times.Exactly(3)))
    .Activity<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), (_, results) => ActivityTransforms.TotalItems(results), configure: options => options.Times(Times.Once()))
    .SubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), (_, input) => Task.FromResult(input))
    .StartAsync(request);

// Wait for every plan to be assigned, then finish the handlers in C/B/A order.
await processItems.WaitForAllInvocationsAsync();
await processItems[2].WaitForCompletionAsync(TimeSpan.FromSeconds(5));
await processItems[1].ReleaseAsync(TimeSpan.FromSeconds(5));
await processItems[0].ReleaseAsync(TimeSpan.FromSeconds(5));

// Fan-in retains A/B/C input order, regardless of handler completion order.
var expected = new BatchResult("BATCH-7", 120m, [new("A", 20m), new("B", 40m), new("C", 60m)]);
await test.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput(expected));
Assert.That(processItems.CompletionOrder.Select(item => item.Id), Is.EqualTo(new[] { "C", "B", "A" }));
processItems.Verify();
test.Verify();
```

`.After(...)` is a real wall-clock activity delay. It does not advance orchestration time. Response completion signals identify handler completion, not the order in which the engine processes the results. The [original full test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs) additionally keeps the child pending, observes `Finalizing` while running, and sends an event to the child's instance through the SDK client.

## Arrange activity responses

| API | Meaning |
|---|---|
| `ActivityMock.ByMatch([ActivityResponse.Match<TInput,TOutput>(predicate, factory), ...])` | Each invocation must match exactly one unused plan. Zero or multiple matches fail the activity; there is no ordered fallback. |
| `ActivityMock.InOrder([ActivityResponse.Next<TInput,TOutput>(factory), ...])` | Consume plans by observed handler arrival, not orchestration source order. Useful for repeated calls/retries; exhaustion fails the activity. |
| No completion modifier | Return the factory's value immediately. A throwing factory fails that activity attempt. |
| `.Pending()` / `mock[index].ReleaseAsync(timeout)` | Hold until explicitly released; releasing also waits for the handler to finish. |
| `.After(duration)` | Return after a real-time delay. Cannot be combined with `.Pending()`. |
| `mock[index]` | Zero-based declaration position in either matching mode, not observed invocation order. |
| `mock.WaitForAllInvocationsAsync(timeout?)` | Wait for every plan to be assigned, not completed; default five seconds. |
| `mock.Verify()` | Check that every plan was assigned, including failed attempts or responses still pending completion. |

Plans freeze when attached to a mock and cannot be reused, shared or configured twice. Each attempt consumes one plan, including retries. In match mode, overlapping unused predicates are ambiguous even when intended for successive retries; use `InOrder` for repeated identical inputs. `using` disposes a mock and cancels blocked gates, delays and response waiters; it does not stop synchronous output factories already executing.

Register `mock.InvokeAsync` through `.Activity(...)`. `configure: options => options.Times(...)` sets registered activity-count expectations, checked explicitly by `test.Verify()` after completion. Without an override, verification expects at least one call. This is separate from checking individual response-plan consumption. Neither verification runs automatically.

## Events, status and assertions

For the real approval orchestration, an event can win while its unchanged 24-hour timer remains pending:

```csharp
using var functions = FunctionTester.Create<WorkflowActivities>();
await using var test = await functions.DurableTask()
    .Orchestrator<ApprovalRequest, ApprovalDecision>(
        nameof(ApprovalOrchestration.AwaitApproval),
        (context, _) => ApprovalOrchestration.AwaitApproval(context))
    .StartAsync(new ApprovalRequest(TimeSpan.FromHours(24)));

await test.WaitForStartAsync();
await test.RaiseEventAsync("Approved", new ApprovalDecision(true));
await test.WaitForCompletionAsync(assert: result => result.AssertOutput(new ApprovalDecision(true)));
```

See the [executed approval test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs). Waiting for start is optional: the pinned SDK host buffers early events. This does not establish exact same-deadline event/timer arbitration.

`WaitForStartAsync`, `WaitForCompletionAsync` and `WaitForCustomStatusAsync` accept a nullable timeout, default to five seconds, and reject nonpositive overrides. `WaitForCustomStatusAsync(metadata => ...)` polls SDK metadata and returns the matching snapshot; inspect status through `metadata.ReadCustomStatusAs<T>()`. See the in-flight status example in [the full batch test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs).

`WaitForCompletionAsync` returns an assertor even for failed orchestration metadata; a completed wait does not mean orchestration success. Use `AssertCompleted()`, `AssertOutput(expected, pathsToIgnore)`, or `AssertFailed().AssertFailureCausedBy<TException>()`. `Output`, `Status`, `Metadata` and `FailureDetails` remain accessible. Failure details are SDK `TaskFailureDetails`, including nested causes, not rethrown original activity exceptions.

Expectations such as `test.ExpectSuccess().ExpectLogContains(...)` run after completion diagnostics and before the optional assertion callback. For a failed run, expectations receive a `DurableTaskOrchestrationException` containing its SDK metadata. Callback exceptions propagate unchanged. Use `test.Host.Client` for advanced SDK operations, including events addressed to a child instance; these escape hatches are not additional Functions-runtime guarantees.

## Activity retries

Activity calls carrying `TaskOptions.FromRetryPolicy(...)` use the SDK host's real retry behavior, including retry delays and exhaustion. The [ordered-response retry test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs) (`DurableRetryConsumesNextScriptedResponse`) throws on the first attempt, returns `"recovered"` on the second, asserts the completed output and verifies exactly two activity invocations and consumption of both responses.

The [full host test](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs) also runs the real `BatchOrchestration.ProcessBatch` policy: one failure recovers on attempt two; repeated failures stop after three total attempts and leave a failed orchestration with the nested activity failure details. The fan-out/fan-in showcase above does not trigger retries.

Each retry consumes another mock response and counts as another registered activity invocation. Use `ActivityMock.InOrder(...)` for successive outcomes with identical input; duplicate unused `ByMatch` predicates would be ambiguous. Retry intervals use **wall-clock time**, not virtual time, and are not shortened by the tester. These are SDK-host scenarios, not evidence of Azure Functions worker/backend retry delivery.

## Real activities and application DI

The fluent builder registers delegates explicitly; it does not discover `[Function]` methods or automatically bind their arguments. A delegate may invoke a real static method, resolve an instance with application services, or substitute behavior. For an instance activity requiring scoped services and an optional context:

```csharp
// Imports: Microsoft.Extensions.DependencyInjection and UnitTestEx.Azure.Functions.
// Supply this delegate to .Activity<WorkItem, ItemResult>(...).
async (_, item) =>
{
    using var scope = functions.Services.CreateScope();
    var activity = ActivatorUtilities.CreateInstance<WorkflowActivities>(scope.ServiceProvider);
    return await activity.ProcessItem(item, new TestFunctionContext(scope.ServiceProvider));
}
```

This is the arrangement exercised in [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs), not worker-created `FunctionContext` or trigger middleware execution. `.ConfigureHostServices(...)` configures the separate SDK host, not the captured application's service collection. Typed SDK `TaskOrchestrator<TInput,TOutput>` registration uses the lower-level `StartDurableTaskTestHostAsync` registry callback; the fluent function-delegate API does not provide typed-class discovery.

## Timers and virtual time

**The SDK host has no supported virtual clock.** Supported clock control is requested in [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843). Its orchestration timers use wall-clock time; the fluent function-based registrations forward timers unchanged and emit a replay-safe warning. Neither injecting `TimeProvider` nor using `.After(...)` controls engine timers. Never silently shorten production delays. An explicit short timeout input tests that short value; an external event winning a 24-hour race tests the event path, not the eventual 24-hour timeout.

For method-level timer/event tests only, `VirtualTimeOrchestrationContext.AdvanceBy(...)` can advance a long timeout without waiting. Its [tests](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalVirtualTimeTest.cs) cover the real approval body, but the context has no durable engine, serialization boundary, history or replay (`IsReplaying` is always false). It is not interchangeable with the host and cannot run the batch orchestration.

## Output, lifetime and running the example

Completion and timeout reports have `=== BEGIN DURABLE ORCHESTRATION TESTER ===` / `=== END DURABLE ORCHESTRATION TESTER ===` bookends with blank lines inside and outside. Reports show identity, serialized input, captured logs, elapsed time, status, custom status/output and nested failure details when available. Terminal output appears once per run before expectations and callbacks. SDK lifecycle output may appear separately; it is not suppressed or merged into a guaranteed chronology.

A completion timeout reports the last observable status without claiming orchestration failure; metadata retrieval has an additional 250 ms bound. It does not terminate the orchestration. Logs retained at timeout remain available to later expectations, and a later terminal wait still writes its terminal report. Start/custom-status waits do not produce completion reports.

Use `await using` for the run, `using` for the activity mock and owning FunctionTester. Host disposal is not proof of activity/child cancellation; tests with user-owned blocking work must arrange their own cleanup. Do not run independent orchestration runs concurrently against one FunctionTester when relying on its shared captured-log assertions.

Run the showcase from this checkout on .NET 10 (restore the test project for that target first if its assets are missing):

```powershell
dotnet test tests\UnitTestEx.Azure.Functions.Durable.Test\UnitTestEx.Azure.Functions.Durable.Test.csproj --no-restore -f net10.0 -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~DurableTaskShowcaseTest
```

No Functions worker, Core Tools, storage emulator or external Durable backend is required. The in-memory host does start local HTTP/gRPC infrastructure. The repository uses project references in its [Durable test project](../tests/UnitTestEx.Azure.Functions.Durable.Test/UnitTestEx.Azure.Functions.Durable.Test.csproj).

## Support matrix

**Direct invocation**, **SDK host**, and **method-level context** identify different test paths, not interchangeable engines. The table records implemented APIs and the specific scenarios demonstrated by their tests. **Not established** means untested or only partially tested; do not infer support from SDK APIs, sample code, or a nearby passing test. Runtime evidence in this matrix is **.NET 10 only**.

| Durable Functions type or capability | What is demonstrated | What is not supported or remains unproven |
|---|---|---|
| **Activity functions - direct invocation** | Existing `FunctionTester` invokes real activity methods using `Trigger<T>()`. Tests cover static/instance methods, sync/async returns, DTO/collection inputs, DI, supplied `FunctionContext`, logs and original exceptions. See [`DurableActivityTest`](../tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs). | No Functions worker or Durable backend. Does not prove indexing, binding conversion, runtime delivery or Durable activity-failure translation. |
| **Function-based orchestration methods - SDK host** | Real static `ProcessBatch`, instance `FinalizeBatch` and failure orchestration bodies execute through public SDK registration, with observed replay. The fluent `FunctionTester.DurableTask()` facade runs real recovery, decline and approval bodies, exposes SDK metadata/failures and adds timer diagnostics. See [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs) and [`FluentDurableTaskTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs). | Does not exercise the Azure Functions worker/runtime, trigger pipeline or Durable Functions extension input converters. Registration is explicit, not automatic function discovery. |
| **SDK class-based `TaskOrchestrator<TIn,TOut>` - SDK host** | A typed `TaskOrchestrator<string,string>` executes through `DurableTaskRegistry.AddOrchestrator<T>()`, schedules an activity and returns output. Activity failure produces nested `TaskFailedException` and `WorkflowRejectedException` details. See [`ClassBasedOrchestrationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ClassBasedOrchestrationTest.cs). | Standalone SDK behavior, not a class-based Functions worker path. DI-driven orchestrator construction and a fluent typed-class registration API are not established. Use the low-level `StartDurableTaskTestHostAsync` registry callback. |
| **Activity calls from an orchestration - SDK host and selected direct-method probes** | Registered delegates invoke real activity methods using application scopes, DI, captured logs and `TestFunctionContext`. Batch tests inspect the three-attempt, one-second initial retry policy; host tests observe recovery after one failure and parent failure after three failed attempts. | No Functions worker. Retry evidence applies to the pinned SDK host and tested policies. The context is test-supplied, not worker middleware. |
| **Sub-orchestrations - SDK host** | Real `FinalizeBatch` executes; `SubOrchestrator(...)` registers child delegates. Tests cover child success/failure, retry recovery on attempt three, exhaustion after three attempts, and nested causes. A pending child completes or fails after parent termination without changing the parent's `Terminated` status. | These cases do not establish general nested-orchestration/cancellation behavior or Functions child delivery. The virtual-time context rejects child calls. |
| **Fan-out/fan-in - SDK host and selected direct-method probes** | The full [`FluentBatchOrchestrationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs) controls the finalization child; its simpler `ActivityMock` test and the [commented showcase](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs) finish handlers C/B/A while fan-in retains A/B/C input order. A separate direct-context probe also verifies ordered fan-in. | Handler completion is not engine result-processing order or a general deterministic scheduler. The showcase does not execute its mocked activities or substituted child body. |
| **Activity mocks and verification** | `ActivityMock.ByMatch` requires exactly one unused matching plan; `InOrder` consumes plans by handler arrival. Responses complete immediately, after a real delay or on release. Tests cover invalid matches, concurrency, retries, exhaustion, ownership, factory failures and disposal. `mock.Verify()` checks plan consumption; `run.Verify()` checks registered activity counts. | No implicit fallback, plan reuse or automatic verification. Retries consume extra plans. Disposal cannot stop a synchronous factory already executing. Delays are not virtual time. |
| **Durable timers - two distinct behaviors** | The method-level context advances the real 24-hour approval timeout instantly; tests cover due timers, invalid deadlines/advances, multiple timers in one advance and late events. Host tests forward a 100 ms timer, observe a real 100 ms approval timeout and emit the wall-clock warning. See [`ApprovalVirtualTimeTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalVirtualTimeTest.cs), [`ApprovalHostEventTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalHostEventTest.cs), and [`HostTimerDiagnosticTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/HostTimerDiagnosticTest.cs). | The host has no virtual clock and waits in real time. The method-level context has no engine timer history/replay; due timers fire before `AdvanceBy` returns, so atomic same-deadline arbitration is not modeled. Short explicit test inputs do not prove production-duration timers. |
| **Configurable timeout values - application design option** | `ApprovalRequest.Timeout` supplies a serializable duration. One host test uses 100 ms; another retains 24 hours and lets an event win. | No options-injection override is tested. A shorter input tests a different value, not virtual time. `TimeProvider` does not control `TaskOrchestrationContext.CreateTimer`. Avoid mutable options inside replayed code; prefer stable input/versioned values. |
| **External events and timer/event coordination - SDK host and method-level context** | An event wins while the host's 24-hour approval timer is pending. Early events are buffered/deserialized; two same-name events reach successive waits in arrival order; late events leave completed output unchanged. Method tests cover event-before-timeout, timeout, pre-canceled waits, an event one tick before the deadline and same-name waits. | The context helper completes the most recent same-name wait; this is not an engine contract. Exact same-deadline arbitration and Functions event delivery/serializer parity are unproven. |
| **Cancellation - narrow cases** | Context tests cover pre-canceled/pending waits and separate cancellation of same-name waits. Host tests show that a child remains running after parent termination and can later complete/fail; a blocked activity delegate can finish after parent termination. | The pinned `TaskActivityContext` exposes no cancellation-token property. General propagation is unproven; parent termination does not imply activity/child cancellation. |
| **Replay and determinism - SDK host** | Batch/timer tests observe replay and one replay-safe warning. [`ReplayDeterminismTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ReplayDeterminismTest.cs) changes the activity name across replay and checks `NonDeterministicOrchestrationException` with both names. | The virtual context never replays. The host did not promptly diagnose `Task.Delay`, `Task.Run` or `ConfigureAwait(false)` in bounded probes; timeout is not proof of permanent noncompletion. No universal invalid-await detector exists. See [`InvalidAwaitProbe`](../tests/UnitTestEx.DurableFunction.HostProbe/InvalidAwaitProbe.cs). |
| **Input/output serialization and failures - SDK host** | [`DurableSerializationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableSerializationTest.cs) round-trips nullable values, enums and nested collections. `DurableTaskHostTest` verifies caller-input and deserialized-output mutations do not alter stored boundary values. Nested SDK failure details are available. | Host options expose no serializer/`DataConverter` setting; service registration alone does not prove converter consumption. Functions serialization, custom serializer parity and worker failure mapping remain unproven. The virtual context has no boundary serialization. |
| **Custom status - SDK host** | `Finalizing` is visible while the real batch waits on its child and after completion. Tests cover successive updates across durable waits, final retained values and clearing status with `SetCustomStatus(null)`. See [`CustomStatusProgressionTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/CustomStatusProgressionTest.cs). | General update/serialization behavior and Functions-worker behavior are not established by these cases. |
| **Entities, `ContinueAsNew`, durable HTTP and custom retry handlers - not established** | No end-to-end UnitTestEx capability is claimed. The batch's activity retry policy is separately tested in the SDK host. | Some constructs are outside the initial scope; SDK APIs alone do not establish support. The virtual context rejects entities and `ContinueAsNew`. |
| **Client/start/status operations - SDK host** | `StartAsync`, bounded start/completion/custom-status waits, typed events, output/failure assertions and an SDK-client escape hatch are implemented. [`FluentDurableTaskTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs) checks managed completion/timeout diagnostics. | Not Functions starter triggers, production `DurableTaskClient` behavior or HTTP status endpoints. A completion timeout is not orchestration failure or termination. |
| **Host lifecycle - SDK host** | [`PendingOrchestrationDisposalTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/PendingOrchestrationDisposalTest.cs) disposes within five seconds with an event or blocked activity pending; the test then releases its own delegate. [`OrchestrationTerminationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/OrchestrationTerminationTest.cs) observes terminated parent metadata. | Not worker shutdown or Azure cleanup. Disposal does not wait for or cancel blocked delegates; general activity/child cancellation is unproven. |
| **Functions worker, bindings and backend integration - separate integration tier** | Existing activity tests invoke methods directly. A real-host Durable test belongs in a separate opt-in Aspire/Core Tools integration project. | The SDK host and method-level context do not prove indexing, binding, worker startup, storage behavior, Azure authentication or deployed backend behavior. |

## Dependency and framework boundary

The consolidated project targets **.NET 8, 9, 10 and 11**. Runtime and local package-content evidence is **.NET 10 only**; full multi-framework validation is tracked in the [design checklist](./azure-durable-functions-design.md#implementation-status-and-remaining-work).

The project pins `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1** and `Microsoft.DurableTask.Abstractions` **1.28.0**. The Microsoft host has `net6.0`, `net8.0` and `net10.0` assets; nearest-framework selection uses net8 assets for net9 and net10 assets for net11, but those combinations have not been built or tested here. Abstractions 1.28.0 supplies a `netstandard2.0` asset. The recorded net10 graph resolves shared Durable Task SDK packages to **1.28.0** and `Microsoft.Azure.DurableTask.Core` to **3.11.2**; asset/graph inspection alone does not validate other frameworks.

Both testing modes share the same host dependency graph. The Durable package references regular-version `UnitTestEx.Azure.Functions`; the existing Functions package does not acquire a Durable dependency. NuGet package versioning adds `-preview` to the shared base version without changing numeric assembly versioning. The pinned upstream source and detailed dependency evidence remain in the [design record](./azure-durable-functions-design.md).
