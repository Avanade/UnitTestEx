# Azure Durable Functions testing

**Preview disclaimer:** This package is intentionally marked as preview because it depends on Microsoft's preview `Microsoft.DurableTask.InProcessTestHost` package. Neither the dependency nor this integration should be treated as a stable API contract: **breaking changes may occur at any time**, including between preview releases.

The optional **`UnitTestEx.Azure.Functions.Durable`** preview package includes the in-memory SDK-host facade, activity mocks and the separate method-level virtual-time context. Its package version is `$(Version)-preview`, using the shared `Common.targets` version: currently **`6.2.0-preview`**. Use regular **`6.2.0`** versions of the other UnitTestEx packages, including your test-framework package. All packages release as a coordinated set; a shared version bump to `6.2.1` produces Durable `6.2.1-preview`, not an independently numbered preview.

**Clock limitation:** The SDK host has no supported virtual-clock control; orchestration timers and retry delays use wall-clock time. Our upstream request is tracked in [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843). The separate method-level virtual-time context does not change the SDK host's clock; see [timers and virtual time](#timers-and-virtual-time).

Add the package alongside your chosen UnitTestEx test-framework integration:

```xml
<PackageReference Include="UnitTestEx.Azure.Functions.Durable" Version="6.2.0-preview" />
```

The host API uses the `UnitTestEx.Azure.Functions.Durable.TestHost` namespace; this is not a separate package. `VirtualTimeOrchestrationContext` is in `UnitTestEx.Azure.Functions.Durable`. Libraries target **.NET 8/9/10/11**; the dedicated orchestration/context suite targets **.NET 10/11**.

[Back to README](../README.md) | [Support matrix](#support-matrix) | [Architecture and design](./azure-durable-functions-design.md)

## Choose the right test

| Question | Test path | Boundary |
|---|---|---|
| Does an activity method use its services and return the right value? | Existing `FunctionTester.Trigger<T>()`; see [activity invocation](./azure-functions.md#durable-activity-invocation). | Direct invocation, not Durable scheduling or failure translation. |
| Does the real orchestration coordinate activities, children, events and failures? | Preview `FunctionTester.DurableTask()` in the optional Durable package. | The standalone in-memory Durable Task SDK engine, including its serialization and replay; not the Functions worker. |
| Does timer/event coordination work when advancing a long timeout instantly? | `VirtualTimeOrchestrationContext` in the optional Durable package. | Narrow method-level context, not engine scheduling, history or replay. It rejects activity and child calls. |
| Can Functions discover, bind and run the workflow against a backend? | Separate opt-in [Functions/Azurite integration suite](#real-functions-workerbackend-integration). | Real Functions worker and Azure Storage provider with local emulator storage, not deployed Azure services. |

The existing Functions package remains independent of Durable Task. The optional Durable package references it, not the other way around. Both testing modes share this one package and its host dependencies; they remain distinct execution models. The host pins `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1**; see [dependencies and frameworks](#dependency-and-framework-boundary).

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
| `ActivityMock.InOrder([ActivityResponse.Next<TInput,TOutput>(factory), ...])` | Consume plans by handler arrival, not orchestration source order. Useful for repeated calls/retries; exhaustion fails the activity. |
| No completion modifier | Return the factory's value immediately. A throwing factory fails that activity attempt. |
| `.Pending()` / `mock[index].ReleaseAsync(timeout)` | Hold until explicitly released; releasing also waits for the handler to finish. |
| `.After(duration)` | Return after a real-time delay. Cannot be combined with `.Pending()`. |
| `mock[index]` | Zero-based declaration position in either matching mode, not invocation order. |
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

See the [approval example](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs). Waiting for start is optional: the pinned SDK host buffers early events. Do not rely on a particular winner when an event and timer share the exact deadline.

`WaitForStartAsync`, `WaitForCompletionAsync` and `WaitForCustomStatusAsync` accept a nullable timeout, default to five seconds, and reject nonpositive overrides. `WaitForCustomStatusAsync(metadata => ...)` polls SDK metadata and returns the matching snapshot; inspect status through `metadata.ReadCustomStatusAs<T>()`. See the in-flight status example in [the full batch test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs).

`WaitForCompletionAsync` returns an assertor even for failed orchestration metadata; a completed wait does not mean orchestration success. Use `AssertCompleted()`, `AssertOutput(expected, pathsToIgnore)`, or `AssertFailed().AssertFailureCausedBy<TException>()`. `Output`, `Status`, `Metadata` and `FailureDetails` remain accessible. Failure details are SDK `TaskFailureDetails`, including nested causes, not rethrown original activity exceptions.

Expectations such as `test.ExpectSuccess().ExpectLogContains(...)` run after completion diagnostics and before the optional assertion callback. For a failed run, expectations receive a `DurableTaskOrchestrationException` containing its SDK metadata. Callback exceptions propagate unchanged. Use `test.Host.Client` for advanced SDK operations, including events addressed to a child instance; these escape hatches are not additional Functions-runtime guarantees.

## Activity and child retries

Activity calls carrying `TaskOptions.FromRetryPolicy(...)` use the SDK host's real retry behavior, including retry delays and exhaustion. The [ordered-response retry test](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs) (`DurableRetryConsumesNextScriptedResponse`) throws on the first attempt, returns `"recovered"` on the second, asserts the completed output and verifies exactly two activity invocations and consumption of both responses.

The [full host test](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs) also runs the real `BatchOrchestration.ProcessBatch` policy: one failure recovers on attempt two; repeated failures stop after three total attempts and leave a failed orchestration with the nested activity failure details. The fan-out/fan-in showcase above does not trigger retries.

Each retry consumes another mock response and counts as another registered activity invocation. Use `ActivityMock.InOrder(...)` for successive outcomes with identical input; duplicate unused `ByMatch` predicates would be ambiguous. Retry intervals use **wall-clock time**, not virtual time, and are not shortened by the tester. These scenarios use SDK-host scheduling, not Functions worker/backend delivery.

Activity and child calls also accept `TaskOptions.FromRetryHandler(...)`. A synchronous handler can select eligible failures and bound the number of attempts:

```csharp
TaskOptions.FromRetryHandler(retry =>
    retry.LastAttemptNumber < 3 && retry.LastFailure.IsCausedBy<WorkflowRejectedException>())
```

An asynchronous handler may await durable tasks using `retry.OrchestrationContext`, including a timer before retrying. Handler code has the same determinism constraints as orchestration code: do not use arbitrary I/O, mutable external counters or `Task.Delay`. Timer waits remain real time. `LastFailure.IsCausedBy<T>()` checks that failure's type; walk `InnerFailure` when a child failure wraps the activity's original cause. See the custom-handler scenarios in [activity tests](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs) and [child tests](../tests/UnitTestEx.Azure.Functions.Durable.Test/SubOrchestrationRetryTest.cs).

## Continue as new

Call `context.ContinueAsNew(newInput, preserveUnprocessedEvents: false)` to start a new generation with serialized input and a fresh history under the same instance ID, discarding buffered events. Return immediately afterward.

**The pinned SDK host cannot preserve buffered events across continuation**, including when preservation is requested through the default `true` argument. The host drops the SDK's carryover self-events ([microsoft/durabletask-dotnet#844](https://github.com/microsoft/durabletask-dotnet/issues/844)). The fluent context emits a replay-safe warning when preservation is requested, but forwards the call unchanged; it does not silently fake delivery or resend events. Use the [real Functions integration tier](#real-functions-workerbackend-integration) for preservation behavior.

The fluent run spans generations. `WaitForCompletionAsync` and output assertions use the terminal generation, and `Metadata.ReadInputAs<T>()` reads that generation's input, not necessarily the initially scheduled input. Activity counts and mock-response consumption accumulate across the whole run.

Use an explicit stopping condition or terminate through `run.Host.Client` for an indefinitely continuing orchestration. Completion waits remain bounded and timers remain wall-clock. This feature belongs to the SDK host, not `VirtualTimeOrchestrationContext`. See [continuation examples](../tests/UnitTestEx.Azure.Functions.Durable.Test/ContinueAsNewTest.cs).

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

This is the arrangement exercised in [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs), not worker-created `FunctionContext` or trigger middleware execution. `.ConfigureHostServices(...)` configures the separate SDK host, not the captured application's service collection.

## Typed orchestration instances

Register a `TaskOrchestrator<TInput,TOutput>` instance explicitly; the builder infers its input/output types and retains the same SDK execution, timer warnings, expectations and managed diagnostics as function delegates:

```csharp
using var functions = FunctionTester.Create<WorkflowActivities>();
using var scope = functions.Services.CreateScope();
var orchestrator = ActivatorUtilities.CreateInstance<ClassBasedEchoOrchestrator>(scope.ServiceProvider);
await using var test = await functions.DurableTask()
    .Orchestrator("ClassBasedEcho", orchestrator)
    .Activity<string, string>("ClassBasedEchoActivity", (_, input) => $"processed:{input}")
    .StartAsync("hello");

await test.WaitForCompletionAsync(assert: result => result.AssertOutput("processed:hello"));
```

The example uses [`ClassBasedEchoOrchestrator`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ClassBasedEchoOrchestrator.cs). `.SubOrchestrator(name, instance)` also registers a typed child from either an input or no-input parent builder.

Instances are caller-owned and reused during replay and continuation. Keep their dependencies and DI scope alive until after the run is disposed; keep durable state in input/history rather than mutable instance fields. Registration does not automatically discover types, resolve them from DI or bind Functions arguments. Use the low-level `StartDurableTaskTestHostAsync` registry callback for SDK type/factory registration instead.

## Timers and virtual time

**The SDK host has no supported virtual clock.** Supported clock control is requested in [microsoft/durabletask-dotnet#843](https://github.com/microsoft/durabletask-dotnet/issues/843). Its orchestration timers use wall-clock time; the fluent function-based registrations forward timers unchanged and emit a replay-safe warning. Neither injecting `TimeProvider` nor using `.After(...)` controls engine timers. Never silently shorten production delays. An explicit short timeout input tests that short value; an external event winning a 24-hour race tests the event path, not the eventual 24-hour timeout.

For method-level timer/event tests only, `VirtualTimeOrchestrationContext.AdvanceBy(...)` can advance a long timeout without waiting. Its [tests](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalVirtualTimeTest.cs) cover the real approval body, but the context has no durable engine, serialization boundary, history or replay (`IsReplaying` is always false). It is not interchangeable with the host and cannot run the batch orchestration.

## Output, lifetime and running the example

Completion and timeout reports have `=== BEGIN DURABLE ORCHESTRATION TESTER ===` / `=== END DURABLE ORCHESTRATION TESTER ===` bookends with blank lines inside and outside. Reports show identity, serialized input, captured logs, elapsed time, status, custom status/output and nested failure details when available. Terminal output appears once per run before expectations and callbacks. SDK lifecycle output may appear separately; it is not suppressed or merged into a guaranteed chronology.

A completion timeout reports the last observable status without claiming orchestration failure; metadata retrieval has an additional 250 ms bound. It does not terminate the orchestration. Logs retained at timeout remain available to later expectations, and a later terminal wait still writes its terminal report. Start/custom-status waits do not produce completion reports.

Use `await using` for the run, `using` for the activity mock and owning FunctionTester. Host disposal does not cancel activity/child work; tests with user-owned blocking work must arrange their own cleanup. Do not run independent orchestration runs concurrently against one FunctionTester when relying on its shared captured-log assertions.

Run the showcase from this checkout on .NET 10 (restore the test project for that target first if its assets are missing):

```powershell
dotnet test tests\UnitTestEx.Azure.Functions.Durable.Test\UnitTestEx.Azure.Functions.Durable.Test.csproj --no-restore -f net10.0 -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~DurableTaskShowcaseTest
```

No Functions worker, Core Tools, storage emulator or external Durable backend is required. The in-memory host does start local HTTP/gRPC infrastructure. The repository uses project references in its [Durable test project](../tests/UnitTestEx.Azure.Functions.Durable.Test/UnitTestEx.Azure.Functions.Durable.Test.csproj).

## Real Functions worker/backend integration

The separate [`UnitTestEx.Azure.Functions.Durable.Integration.Test`](../tests/UnitTestEx.Azure.Functions.Durable.Integration.Test) project and [`UnitTestEx.Aspire.Durable.AppHost`](../tests/UnitTestEx.Aspire.Durable.AppHost) run the existing sample through **Core Tools, its real .NET 10 isolated worker, the Durable Functions extension and the Azure Storage provider backed by Azurite**. No application startup changes, substituted activities, SDK test-host dependency or test-only starter function are required.

| Scenario | Real application path and assertions |
|---|---|
| Batch | `ProcessBatch` invokes normalization, three item activities, aggregation and the instance `FinalizeBatch` child, including journal record/publish activities. Assert `BATCH-7`, amounts 20/40/60 in A/B/C order, total 120, final custom status and worker/journal logs. |
| Recovery | `RecoverWorkflow` catches the real `RejectWorkflow` activity failure and returns `Declined`, with matching custom status. Expected error-level logs are permitted only in this failure fixture. |
| Approval | Start `AwaitApproval` with its unchanged 24-hour timeout, observe `Running`, send `Approved` through the extension's HTTP endpoint and assert approval completion. This tests event delivery, not eventual 24-hour timer completion or virtual time. |
| Continuation | Buffer an external payload before `ContinueAsNew`, then assert preservation or discard through the real extension and storage provider, including the next generation's input and output. |

Tests first verify indexed functions through `WaitForFunctionsAsync`. They use the extension's [documented HTTP management API](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-http-api) to schedule instances, poll terminal state, send events and purge completed parent history. The returned management URLs are restricted to the test host. A missing initial status is retried because scheduling can be acknowledged before storage exposes the instance; other unexpected responses report their bodies. Polling is bounded, and failed terminal states are asserted rather than treated as successful completion.

Each test owns a fresh AppHost, random task hub and nonpersistent Azurite **3.37.0** container. This emulator supports the extension's Blob REST API version without disabling API-version validation. AppHost disposal stops its worker and removes its container, including child history or pending timer data, even when an assertion fails. No shared storage account or persistent mount is used. Test-only configuration reduces **storage queue polling** to one second; application timer durations are not shortened. Host diagnostics retain the worker and storage console output for failure context.

This suite requires **Azure Functions Core Tools v4 and an Aspire-compatible container runtime**. It uses ports **7073 and 11000–11002**; do not run concurrent copies on the same execution host. The test project and hosted application both target .NET 10.

To opt into the integration job, dispatch CI with **`durable-integration: true`** (the default is false):

```powershell
gh workflow run ci.yml --repo Avanade/UnitTestEx --ref feature/durable-functions-testing -f durable-integration=true
```

This is local-emulator integration, not deployed Azure authentication, scale, reliability or publishing-service coverage. The sample journal still only logs. The in-memory suite and method-level context remain separate and container-free.

## Support matrix

**Direct invocation**, **SDK host**, **method-level context** and **Functions integration** are different test paths, not interchangeable engines. Choose the path that exercises the behavior your test needs.

| Durable Functions type or capability | Test API and behavior | Limitations |
|---|---|---|
| **Activity functions - direct invocation** | `FunctionTester.Trigger<T>()` invokes static/instance activity methods with sync/async returns, typed inputs, application DI, supplied `FunctionContext`, captured logs and original exceptions. See [`DurableActivityTest`](../tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs). | No Functions worker, binding conversion, runtime delivery or Durable activity-failure translation. |
| **Function-based orchestration methods - SDK host** | Register static/instance orchestration bodies through `FunctionTester.DurableTask()` for SDK execution, replay, metadata/failures and timer diagnostics. See [`DurableTaskHostTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs) and [`FluentDurableTaskTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs). | No Azure Functions worker/runtime, trigger pipeline or extension input converters. Registration is explicit, not automatic function discovery. |
| **SDK class-based `TaskOrchestrator<TIn,TOut>` - SDK host** | `.Orchestrator(name, instance)` and `.SubOrchestrator(name, instance)` retain typed input/output, managed diagnostics and nested failures. SDK type/factory registration remains available through the low-level registry. See [`ClassBasedOrchestrationTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ClassBasedOrchestrationTest.cs). | Caller owns the instance and dependencies; no automatic discovery or Functions class-based worker integration. |
| **Activity calls from an orchestration - SDK host** | Registered delegates invoke real activities through application scopes, DI, captured logs and `TestFunctionContext`, or supply mock responses. SDK retry policies apply to failed attempts. | No Functions worker; context and scope creation are caller-owned. Retry delays use wall-clock time. |
| **Sub-orchestrations - SDK host** | `SubOrchestrator(...)` registers real or substitute child delegates with SDK success/failure, retry and nested-failure handling. | Parent termination does not cancel child work. The virtual-time context rejects child calls. |
| **Fan-out/fan-in - SDK host** | `ActivityMock` controls response completion while `Task.WhenAll` preserves input/task order. See the [commented showcase](../tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs). | Handler completion is not engine result-processing order. Mocked activity and child bodies are not executed. |
| **Activity mocks and verification** | `ActivityMock.ByMatch` requires exactly one unused matching plan; `InOrder` consumes plans by handler arrival. Responses complete immediately, after a real delay or on release. `mock.Verify()` checks plan consumption; `run.Verify()` checks registered activity counts. | No implicit fallback, plan reuse or automatic verification. Retries consume extra plans. Disposal cannot stop a synchronous factory already executing. Delays are not virtual time. |
| **Durable timers - two distinct behaviors** | Host timers use real time with replay-safe warnings. `VirtualTimeOrchestrationContext.AdvanceBy(...)` advances method-level timer waits without sleeping. See [`ApprovalVirtualTimeTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalVirtualTimeTest.cs). | No host virtual clock. The context has no engine timer history/replay; due timers fire before `AdvanceBy` returns and atomic same-deadline arbitration is not modeled. |
| **Configurable timeout values - application design option** | Supply serializable durations through orchestration input, such as `ApprovalRequest.Timeout`. | A shorter input is a different value, not virtual time. `TimeProvider` does not control `CreateTimer`. Prefer stable input/versioned values over mutable options in replayed code. |
| **External events and timer/event coordination** | Use SDK-host `RaiseEventAsync` or method-level context `RaiseEvent`/`AdvanceBy`; the host buffers early events. | The context helper completes the most recent same-name wait and is not an engine event-delivery contract. Do not depend on exact same-deadline event/timer ordering. |
| **Cancellation** | The method-level context accepts cancellation tokens for timer/event waits. SDK client access permits orchestration termination. | The pinned `TaskActivityContext` has no cancellation-token property. Parent termination does not imply activity/child cancellation; clean up caller-owned work explicitly. |
| **Replay and determinism - SDK host** | SDK history replay and `NonDeterministicOrchestrationException` for divergent durable calls. See [`ReplayDeterminismTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/ReplayDeterminismTest.cs). | The virtual context never replays. The host is not a universal invalid-await detector; follow Durable orchestration constraints and use the upstream analyzer. |
| **Input/output serialization and failures - SDK host** | SDK serialization for inputs/outputs and nested `TaskFailureDetails` for activity/child failures. | Host options expose no serializer/`DataConverter` setting. This is SDK serialization, not Functions extension conversion. The virtual context has no boundary serialization. |
| **Custom status - SDK host** | Set, inspect and clear status through `SetCustomStatus`, `WaitForCustomStatusAsync` and `ReadCustomStatusAs<T>()`. | Status is SDK-host metadata, not a Functions HTTP status endpoint. |
| **`ContinueAsNew` - SDK host** | New generations with serialized input and discard semantics. Completion uses final-generation output/metadata; activity counts span generations. | Buffered-event preservation is unsupported by the pinned host and requests emit a warning. Return immediately after the call. No continuation/history-reset model in the virtual context or host virtual clock. |
| **Custom retry handlers - SDK host** | Synchronous/asynchronous SDK handlers for activity and child calls, using failure details, attempt numbers and durable waits. | Handler code must be deterministic. Nested child failure details require explicit traversal; timer waits remain wall-clock. |
| **Entities and durable HTTP** | Outside the Durable tester's higher-level supported surface; use Functions-runtime integration for extension behavior. | No built-in SDK-host entity-testing/HTTP implementation or method-level context support. |
| **Client/start/status operations - SDK host** | `StartAsync`, bounded start/completion/custom-status waits, typed events, output/failure assertions and an SDK-client escape hatch are implemented. [`FluentDurableTaskTest`](../tests/UnitTestEx.Azure.Functions.Durable.Test/FluentDurableTaskTest.cs) checks managed completion/timeout diagnostics. | Not Functions starter triggers, production `DurableTaskClient` behavior or HTTP status endpoints. A completion timeout is not orchestration failure or termination. |
| **Host lifecycle - SDK host** | Dispose the host/run with `await using`; terminate instances through the SDK client. | Not worker shutdown or Azure cleanup. Disposal does not wait for or cancel blocked delegates. |
| **Functions worker, bindings and backend integration** | Separate opt-in [Core Tools/Azurite scenarios](#real-functions-workerbackend-integration) for real batch activities/child, failure recovery, approval-event delivery and continuation with buffered-event preservation/discard. | Requires Core Tools and a container runtime. Local emulator integration does not cover deployed Azure authentication, scale or reliability. |

## Build and test configuration

The [workflow](../.github/workflows/ci.yml) runs independent framework jobs and uploads test results even when a job fails. The Functions integration job is opt-in.

| Project or suite | Frameworks |
|---|---|
| Libraries and core MSTest/NUnit/xUnit suites | .NET 8/9/10/11 |
| Aspire MSTest/NUnit/xUnit suites | .NET 10/11 |
| Dedicated Durable orchestration/context suite | .NET 10/11 |
| Fixed Aspire AppHosts, resources and Durable Functions integration | .NET 10 |

## Dependency and framework boundary

The consolidated library targets **.NET 8, 9, 10 and 11**. The dedicated Durable test suite targets **.NET 10/11**; real Functions integration uses **.NET 10**.

The project pins `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1** and `Microsoft.DurableTask.Abstractions` **1.28.0**. The Microsoft host has `net6.0`, `net8.0` and `net10.0` assets; nearest-framework selection uses net8 assets for net9 and net10 assets for net11. Abstractions 1.28.0 supplies a `netstandard2.0` asset.

Both testing modes share the same host dependency graph. The Durable package references regular-version `UnitTestEx.Azure.Functions`; the existing Functions package does not acquire a Durable dependency. NuGet package versioning adds `-preview` to the shared base version without changing numeric assembly versioning. See [architecture and design](./azure-durable-functions-design.md).
