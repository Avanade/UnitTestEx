# UnitTestEx.Azure.Functions.Durable (preview)

Optional **7.0.0-preview** package for fluent in-memory Durable Task SDK-host tests, activity mocks, invocation verification, managed diagnostics and a separate method-level virtual-time context. Regular UnitTestEx dependencies use **7.0.0**.

**Preview and scope:** This evolving implementation targets key testing scenarios, not exhaustive Durable Functions coverage. It depends on Microsoft's preview `Microsoft.DurableTask.InProcessTestHost` 0.2.3-preview.1; breaking changes may occur between preview releases. [Request additional scenarios](https://github.com/Avanade/UnitTestEx/issues/new/choose).

## Quick start

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

Use an ordinary class from your Functions application assembly in place of `WorkflowActivities`. This minimal delegate checks wiring; register delegates to real application orchestrations/activities to test their behavior. The [showcase](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs) runs a real batch body with controlled activity responses.

To run application activities and orchestrations without forwarding delegates, use `functions.DurableTask().WithSelfDiscovery()`. It scans only the parent marker's assembly; `WithSelfDiscovery<T>()` adds another marker's assembly. Explicit registrations override discoveries. Direct orchestration runs select a root with `.Orchestrator(...)`; for a starter function, use `.StartHostAsync()`, pass `session.Client` explicitly, and observe its known instance ID with `session.Observe<TOutput>(id)`. See the [starter example](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md#test-a-starter-function). Dispose the run before the session and FunctionTester. Real instance activities use owned invocation scopes; discovered instance orchestrations retain separate scopes across replay until session/run disposal. Duplicate names/unsupported signatures fail early. Real activities may have external side effects.

## Boundaries

- SDK-host orchestration timers and retry delays use wall-clock time, not virtual time.
- `VirtualTimeOrchestrationContext` is a separate method-level timer/event tool without engine history/replay or activity/child execution.
- The pinned SDK host has a buffered-event preservation limitation across `ContinueAsNew`; use real-worker integration to test preservation.
- SDK-host tests do not run the Functions worker or prove binding/serializer parity. Ordinary activity-method tests use `FunctionTester.Trigger<T>()` without this optional package.
- Starter tests invoke the method directly with an SDK-host client; they do not exercise Service Bus delivery or `[DurableClient]` binding.

## AI assistance and documentation

The package includes `skills/unittestex-durable/SKILL.md`, version-matched to these APIs and boundaries. See [discovery and optional installation](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/ai-support.md).

- [Durable guide and support matrix](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md)
- [Architecture](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions-design.md)
- [Real Functions integration](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md#real-functions-workerbackend-integration)
