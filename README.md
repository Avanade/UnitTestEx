<br/>

![Logo](./images/Logo256x256.png "UnitTestEx")

<br/>

## Introduction

_UnitTestEx_ provides [.NET testing](https://learn.microsoft.com/en-us/dotnet/core/testing/) extensions for [MSTest](https://github.com/Microsoft/testfx-docs), [NUnit](https://nunit.org/) and [xUnit](https://xunit.net/). It hosts your **real** application (API, Azure Function, or any DI-composed code) inside the test, lets you replace or mock its dependencies, and gives you fluent _Arrange, Act, Assert_ testing with captured logging and detailed JSON-diff assertions.

> **v6** targets .NET 8, 9, 10 and (from v6.2) 11, and the Azure Functions **v4 isolated worker** model only. Upgrading? See [Migrating from v5 to v6](./docs/migration-v6.md).

<br/>

## Status

The build status is [![CI](https://github.com/Avanade/UnitTestEx/workflows/CI/badge.svg)](https://github.com/Avanade/UnitTestEx/actions?query=workflow%3ACI) with the NuGet package status as follows, including links to the underlying source code and documentation:

Project/Package | Description | Source | Docs
-|-|-|-
`UnitTestEx`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.svg)](https://badge.fury.io/nu/UnitTestEx) | Core API and DI-composed service testers, with HTTP-client mocking, dependency overrides, fluent assertions and captured logging. Use with a test-framework integration package. | [Source](./src/UnitTestEx) | [API testing](./docs/api-testing.md), [Generic testing](./docs/generic-testing.md), [HTTP mocking](./docs/http-client-mocking.md), [DI/configuration](./docs/dependency-injection-and-configuration.md), [Assertions/logging](./docs/assertions-and-expectations.md), [Set-up/extensibility](./docs/test-setup-and-extensibility.md)
`UnitTestEx.Azure.Functions`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.Azure.Functions.svg)](https://badge.fury.io/nu/UnitTestEx.Azure.Functions) | Tests isolated-worker Functions using their normal application startup, scoped services and worker middleware. Invokes function methods without starting the Azure Functions worker/runtime. | [Source](./src/UnitTestEx.Azure.Functions) | [Azure Functions](./docs/azure-functions.md), [DI/configuration](./docs/dependency-injection-and-configuration.md), [Assertions/logging](./docs/assertions-and-expectations.md), [Set-up/extensibility](./docs/test-setup-and-extensibility.md)
`UnitTestEx.Azure.Functions.Durable`<br/>[![NuGet version (preview)](https://img.shields.io/nuget/vpre/UnitTestEx.Azure.Functions.Durable.svg)](https://www.nuget.org/packages/UnitTestEx.Azure.Functions.Durable) | Tests real orchestration bodies through the in-memory Durable Task SDK host, with activity mocks, fluent assertions and diagnostics. Also includes a separate method-level virtual-time context; neither mode exercises the Azure Functions worker/runtime. | [Source](./src/UnitTestEx.Azure.Functions.Durable) | [Durable testing](./docs/azure-durable-functions.md), [Support matrix](./docs/azure-durable-functions.md#support-matrix), [Design/implementation](./docs/azure-durable-functions-design.md)
`UnitTestEx.Aspire`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.Aspire.svg)](https://badge.fury.io/nu/UnitTestEx.Aspire) | Runs multi-service tests against a real .NET Aspire distributed application. Provides resource readiness checks, HTTP helpers, captured resource logs and external HTTP mocking. | [Source](./src/UnitTestEx.Aspire) | [Aspire testing](./docs/aspire.md)
`UnitTestEx.MSTest`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.MSTest.svg)](https://badge.fury.io/nu/UnitTestEx.MSTest) | Connects UnitTestEx assertions and test output to MSTest. Use alongside the core or specialized tester packages. | [Source](./src/UnitTestEx.MSTest) | [Set-up/extensibility](./docs/test-setup-and-extensibility.md), [Assertions/logging](./docs/assertions-and-expectations.md)
`UnitTestEx.NUnit`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.NUnit.svg)](https://badge.fury.io/nu/UnitTestEx.NUnit) | Connects UnitTestEx assertions and test output to NUnit. Use alongside the core or specialized tester packages. | [Source](./src/UnitTestEx.NUnit) | [Set-up/extensibility](./docs/test-setup-and-extensibility.md), [Assertions/logging](./docs/assertions-and-expectations.md)
`UnitTestEx.Xunit`<br/>[![NuGet version](https://badge.fury.io/nu/UnitTestEx.Xunit.svg)](https://badge.fury.io/nu/UnitTestEx.Xunit) | Connects UnitTestEx assertions and test output to xUnit, including its output-helper integration. Use alongside the core or specialized tester packages. | [Source](./src/UnitTestEx.Xunit) | [Set-up/extensibility](./docs/test-setup-and-extensibility.md), [Assertions/logging](./docs/assertions-and-expectations.md)

The [change log](CHANGELOG.md) details all key changes per published version.

<br/>

## Which tester?

Testing... | Use | Package(s) | Hosts | Docs
-|-|-|-|-
An ASP.NET Core API (controllers or minimal APIs) | `ApiTester` | `UnitTestEx` + framework package | `WebApplicationFactory`, full HTTP pipeline | [API testing](./docs/api-testing.md)
An Azure Function (isolated worker) | `FunctionTester` | `UnitTestEx.Azure.Functions` | Your real `Program.cs` host, built but not started; the function is invoked directly | [Azure Functions](./docs/azure-functions.md)
Durable orchestration bodies with replay, activities and children | `FunctionTester.DurableTask()` | `UnitTestEx.Azure.Functions.Durable` (preview) | In-memory Durable Task SDK host; no Functions worker or backend | [Durable testing](./docs/azure-durable-functions.md)
Durable timer/event branches with instant time advancement | `VirtualTimeOrchestrationContext` | `UnitTestEx.Azure.Functions.Durable` (preview) | Method-level context; no engine history/replay, activities or children | [Timers and virtual time](./docs/azure-durable-functions.md#timers-and-virtual-time)
Any DI-composed class or service | `GenericTester` | `UnitTestEx` | Lightweight Generic Host | [Generic testing](./docs/generic-testing.md)
Multiple real services together | `AspireTester` | `UnitTestEx.Aspire` | .NET Aspire `DistributedApplication` | [Aspire](./docs/aspire.md)
Durable Functions worker, bindings and backend interactions | `AspireTester` | `UnitTestEx.Aspire` | Core Tools, isolated worker and a backend/emulator; requires Core Tools and containers for the repository suite | [Real Functions integration](./docs/azure-durable-functions.md#real-functions-workerbackend-integration)

The framework package (`UnitTestEx.NUnit`, `UnitTestEx.MSTest` or `UnitTestEx.Xunit`) wires output and assertions into your test framework.

> **Functions proof boundary:** `FunctionTester` proves invocation behavior, not trigger indexing, binding conversion, broker delivery or final runtime HTTP behavior. Use Aspire/Core Tools with the backing service or emulator for those checks. Posting a BlobCreated webhook simulates notification delivery; it does not test Azure Event Grid. Start with the [Functions guide](./docs/azure-functions.md#proof-boundary-invocation-versus-runtime) and [runnable Blob example](./docs/azure-functions.md#runnable-blob-integration-example).

<br/>

## Quick start

**API**: send a real HTTP request through the pipeline, with a downstream `HttpClient` mocked.

``` csharp
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX", new Uri("https://somesys"))
    .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

using var test = ApiTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .Controller<ProductController>()
    .Run(c => c.Get("abc"))
    .AssertOK()
    .AssertValue(new { id = "Abc", description = "A blue carrot" });
```

**Azure Function**: `Program.cs` runs as-is, with no test-only structure needed.

``` csharp
using var test = FunctionTester.Create<ProductFunction>();
test.ReplaceHttpClientFactory(mcf)
    .HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
    .AssertOK()
    .AssertValue(new { id = "Abc", description = "A blue carrot" });

test.ServiceBusTrigger<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" })))
    .AssertSuccess();
```

**Durable orchestration (preview)**: execute real fan-out/fan-in through the SDK host, with input-matched activity responses released in reverse order. This uses the repository's [sample types](./tests/UnitTestEx.DurableFunction); item processing and the finalization child are substituted, and no Functions worker or backend is started.

``` csharp
using UnitTestEx;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

using var processItems = ActivityMock.ByMatch(
[
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "A", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "B", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
    ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "C", item => new ItemResult(item.Id, item.Amount * 2m)).After(TimeSpan.FromMilliseconds(100))
]);
using var functions = FunctionTester.Create<WorkflowActivities>();
var request = new BatchRequest(" batch-7 ", [new("A", 10m), new("B", 20m), new("C", 30m)]);

await using var test = await functions.DurableTask()
    .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
    .Activity<string, string>(nameof(ActivityTransforms.NormalizeReference), (_, reference) => ActivityTransforms.NormalizeReference(reference))
    .Activity<WorkItem, ItemResult>(nameof(WorkflowActivities.ProcessItem), processItems.InvokeAsync)
    .Activity<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), (_, results) => ActivityTransforms.TotalItems(results))
    .SubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), (_, input) => Task.FromResult(input))
    .StartAsync(request);

await processItems.WaitForAllInvocationsAsync();
await processItems[2].WaitForCompletionAsync(TimeSpan.FromSeconds(5));
await processItems[1].ReleaseAsync(TimeSpan.FromSeconds(5));
await processItems[0].ReleaseAsync(TimeSpan.FromSeconds(5));

var expected = new BatchResult("BATCH-7", 120m, [new("A", 20m), new("B", 40m), new("C", 60m)]);
await test.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput(expected));
processItems.Verify();
```

Fan-in retains A/B/C input order despite C/B/A handler completion. The activity delay is real time, not a virtual-clock advance. See the [complete showcase](./tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs) for completion-order assertions and activity-count verification, or use the [real-worker integration tier](./docs/azure-durable-functions.md#real-functions-workerbackend-integration) to exercise the worker, extension and backend.

**Generic**: any service, with expectations declared up front.

``` csharp
using var test = GenericTester.Create<EntryPoint>();
test.ExpectException().Type<DivideByZeroException>()
    .Run<Gin>(gin => gin.Stir());
```

<br/>

## Capabilities

Each row links to the docs and to a test that shows it working.

Capability | Applies to | Example
-|-|-
Controller tests from a method expression; route, method and body are inferred | API | [`Get_Test1`](./tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs)
Raw HTTP requests, typed responses | API | [`Http_Update_Http2`](./tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs)
`ValidationProblemDetails` / error assertions | API, Function | [`ValidationProblemDetails`](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs)
HTTP trigger: `HttpRequest` → `IActionResult` / `IResult` | Function | [`HttpTrigger_ActionResult_Sync`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`HttpTrigger_Result_Async`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
HTTP trigger: `HttpRequestData` → `HttpResponseData` | Function | [`HttpTrigger_RequestData_Json`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
HTTP method/route validated against `[HttpTrigger]` | Function | [`HttpTrigger_RequestData_RouteCheck`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`WithPathAndQueryCheck`](./tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs)
Multi-output bindings | Function | [`HttpTrigger_MultiOutput`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
Service Bus trigger and message settlement | Function | [`ServiceBusMessage_Success`](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs), [`ServiceBusMessage_AssertActions`](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)
Event Grid (`EventGridEvent` / `CloudEvent`) builders | Function | [`EventGridEvent_FromValue`](./tests/UnitTestEx.NUnit.Test/EventGridExtensionMethodsTest.cs), [`CloudEvent_FromJson`](./tests/UnitTestEx.NUnit.Test/EventGridExtensionMethodsTest.cs)
Worker middleware pipeline (logging, short-circuit, exception mapping) | Function | [`Middleware_ShortCircuit_ActionResult`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_HandlesException_HttpResponseData`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
Other triggers (Timer, Blob, Queue, ...) | Function | [`TimerTriggered`](./tests/UnitTestEx.NUnit.Test/ProductFunctionTest.cs), [`BlobTrigger_Bytes`](./tests/UnitTestEx.NUnit.Test/StorageTriggerTest.cs), [`QueueTrigger_Person`](./tests/UnitTestEx.NUnit.Test/StorageTriggerTest.cs), [`GenericTrigger_ServiceBus`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
Real `Program.cs` with test overrides; worker never started | Function | [`Program_TestOverrides`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Program_ServicesUsed_WorkerNotStarted`](./tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
[Durable activity method invocation with DI, logs and original exceptions](./docs/azure-functions.md#durable-activity-invocation) | Function | [`DurableActivityTest`](./tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs)
[Durable fan-out/fan-in with controlled activity responses](./docs/azure-durable-functions.md#showcase-fan-out-controlled-completion-fan-in) | Durable SDK host | [`BatchFanOutFanIn`](./tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs), [`ActivityMockCanMatchResponsesByInvocationOrder`](./tests/UnitTestEx.Azure.Functions.Durable.Test/FluentBatchOrchestrationTest.cs)
[Durable replay and nested failure diagnostics](./docs/azure-durable-functions.md#support-matrix) | Durable SDK host | [`ReplayDeterminismTest`](./tests/UnitTestEx.Azure.Functions.Durable.Test/ReplayDeterminismTest.cs), [`DurableTaskHostTest`](./tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskHostTest.cs)
[Instant timer/event advancement without engine replay](./docs/azure-durable-functions.md#timers-and-virtual-time) | Durable method-level context | [`ApprovalVirtualTimeTest`](./tests/UnitTestEx.Azure.Functions.Durable.Test/ApprovalVirtualTimeTest.cs)
[Real Durable Functions worker/backend and buffered-event continuation](./docs/azure-durable-functions.md#real-functions-workerbackend-integration) | Aspire + Function | [`DurableFunctionsIntegrationTest`](./tests/UnitTestEx.Azure.Functions.Durable.Integration.Test/DurableFunctionsIntegrationTest.cs)
Run any service, sync or async | Generic | [`Run_Service`](./tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs)
Scoped services (`ScopedType<T>`) | All | [`ScopedType_RunsWithinServiceScope`](./tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs)
Replace / mock DI registrations | All | [`ReplaceSingleton_OverridesRegistration`](./tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs), [`MockSingleton_UsesMoqObject`](./tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs)
Configuration overrides, `appsettings.unittest.json` | All | [`Configuration_Overrride_Use`](./tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs)
`HttpClient` mocking: match, sequence, delay, times, YAML | All | [`MockSequence`](./tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`WithYamlRequests`](./tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
`Expect*` before or `Assert*` after | All | [`Update_Test5_ExpectationFailure`](./tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`ExpectValue_WithFunc`](./tests/UnitTestEx.NUnit.Test/Other/ExpectationsTest.cs)
JSON-diff comparison with paths to ignore | All | [`PathsToIgnore_PropertyName`](./tests/UnitTestEx.NUnit.Test/Other/ObjectComparerTest.cs)
Log capture and log assertions | All | [`Get_Test1_ExpectLogNotContains_Failure`](./tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Checkpoint_WritesReasonAndFlushesLogsToOutput`](./tests/UnitTestEx.NUnit.Test/Other/CheckpointAndDelayTest.cs)
One-off assembly set-up and tester extensions | All | [`TesterExtensions_FunctionTester`](./tests/UnitTestEx.NUnit.Test/Other/OneOffTestSetUpTest.cs)
Multi-host tests with WireMock stubs and log checks | Aspire | [`HttpMock_StubsExternalMockHostDependency_Product`](./tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs), [`ErrorWhenLogContains_DefaultsToEnabled_ThrowsWithoutExplicitRegistration`](./tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs)

<br/>

## Documentation

- [API testing](./docs/api-testing.md)
- [Azure Functions testing](./docs/azure-functions.md), including [limitations](./docs/azure-functions.md#limitations)
- [Durable Functions testing (preview)](./docs/azure-durable-functions.md), including a [commented fan-out/fan-in showcase](./tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs)
- [Generic testing](./docs/generic-testing.md)
- [Dependency injection and configuration](./docs/dependency-injection-and-configuration.md)
- [HTTP client mocking](./docs/http-client-mocking.md)
- [Assertions, expectations and logging](./docs/assertions-and-expectations.md)
- [Test set-up and extensibility](./docs/test-setup-and-extensibility.md)
- [Aspire multi-host testing](./docs/aspire.md)
- [Migrating from v5 to v6](./docs/migration-v6.md)

<br/>

## Examples

The tests in this repository are the reference examples. Core and Aspire examples are grouped by test framework:
- [UnitTestEx.NUnit.Test](./tests/UnitTestEx.NUnit.Test)
- [UnitTestEx.MSTest.Test](./tests/UnitTestEx.MSTest.Test)
- [UnitTestEx.Xunit.Test](./tests/UnitTestEx.Xunit.Test)
- [UnitTestEx.Aspire.Xunit.Test](./tests/UnitTestEx.Aspire.Xunit.Test), [UnitTestEx.Aspire.NUnit.Test](./tests/UnitTestEx.Aspire.NUnit.Test), [UnitTestEx.Aspire.MSTest.Test](./tests/UnitTestEx.Aspire.MSTest.Test)

Durable reference examples use NUnit:
- [SDK-host and method-level context suite](./tests/UnitTestEx.Azure.Functions.Durable.Test) - .NET 10/11; no Core Tools, containers or backend.
- [Real Functions worker/backend integration suite](./tests/UnitTestEx.Azure.Functions.Durable.Integration.Test) - .NET 10; requires Core Tools and a container runtime.

<br/>

## Durable Functions testing (preview)

The optional **`UnitTestEx.Azure.Functions.Durable`** preview package combines the SDK-host facade and the separate method-level virtual-time context. Its version follows `Common.targets` with `-preview` appended: **`6.2.0-preview`**, alongside regular **`6.2.0`** UnitTestEx packages. Libraries target .NET 8/9/10/11; the dedicated SDK-host/context suite targets .NET 10/11, while real-worker integration targets .NET 10. The SDK host pins Microsoft's preview `Microsoft.DurableTask.InProcessTestHost` **0.2.3-preview.1**; preview APIs may change between releases.

The [Durable guide](./docs/azure-durable-functions.md) shows `FunctionTester.DurableTask()`, `ActivityMock`, assertions and diagnostics. The [runnable showcase](./tests/UnitTestEx.Azure.Functions.Durable.Test/DurableTaskShowcaseTest.cs) runs the real batch orchestration with controlled activity responses; see the guide's [support matrix](./docs/azure-durable-functions.md#support-matrix) for APIs and limitations.

Construct | Test path | Behavior | Limitations
-|-|-|-
Activity functions | Existing `FunctionTester.Trigger<T>()` | Real static/instance method invocation, DI, logs and original exceptions | No Functions trigger binding, runtime delivery or Durable failure translation.
Static/instance `[Function]` orchestration methods | In-memory SDK host | Explicit delegates execute real bodies with SDK serialization, replay and failure details | No automatic discovery, Functions worker or extension input conversion.
Typed SDK `TaskOrchestrator<TIn,TOut>` | Fluent SDK host or low-level registry | Explicit root/child instance registration with typed input/output, managed diagnostics and nested failures | Caller owns instances and dependencies; no automatic discovery or Functions class-based worker integration.
Activity calls and fan-out/fan-in | SDK host and `ActivityMock` | Input-matched/ordered responses, controlled completion, input-order fan-in and opt-in invocation verification | Mocked activity bodies are not tested; handler completion is not engine result-processing order.
Sub-orchestrations | SDK host | Real/delegate child registration; selected success, failure and retry scenarios | Not general cancellation propagation or Functions child delivery.
Durable timers | SDK host or method-level virtual context | Host timers use real time with a warning; the narrow context advances timer/event logic instantly | No host virtual clock. Method-level context has no engine history/replay and rejects activities/children.
External events/custom status | SDK host | Selected buffered-event, event-before-timeout and in-flight status scenarios | No Functions delivery/serializer parity or established same-deadline arbitration.
Replay | SDK host | SDK history replay and detection of divergent durable calls | Not a universal detector of invalid awaits or arbitrary I/O; virtual context never replays.
Entities | Outside the Durable tester's supported surface | Existing generic trigger invocation remains unchanged | No entity execution/state API through the host facade.
Client/start/status operations | SDK host/client | Schedule, bounded waits, typed events, metadata and output assertions | Not production `DurableTaskClient`, Functions starter triggers or HTTP status endpoints.
`ContinueAsNew` | SDK host | New generations with serialized input; final output and activity counts across generations | Buffered-event preservation is unsupported by the pinned host ([upstream issue](https://github.com/microsoft/durabletask-dotnet/issues/844)); no method-level virtual-context support or host virtual clock.
Custom retry handlers | SDK host | SDK synchronous/asynchronous handlers for activities and children, including failure details and attempt limits | Handler code must be deterministic; timer waits remain wall-clock.
Durable HTTP | Outside the Durable tester's supported surface | Use a Functions runtime integration for extension-specific HTTP behavior | No built-in SDK-host HTTP implementation or method-level context support.
Functions worker/backend integration | Separate [Aspire/Core Tools/Azurite suite](./docs/azure-durable-functions.md#real-functions-workerbackend-integration) | Real batch activities/child, failure recovery, approval-event delivery and continuation with buffered-event preservation/discard | Requires Core Tools and containers; local emulator integration excludes deployed Azure authentication, scale and reliability.

<br/>

## Other repos

These other _Avanade_ repositories leverage _UnitTestEx_ to provide unit testing capabilities:
- [_CoreEx_](https://github.com/Avanade/CoreEx) - Enriched capabilities for building business services by extending the core capabilities of .NET.
- [_Beef_](https://github.com/Avanade/Beef) - Business Entity Execution Framework to enable industralisation of API development.

<br/>

## License

_UnitTestEx_ is open source under the [MIT license](./LICENSE) and is free for commercial use.

<br/>

## Contributing

One of the easiest ways to contribute is to participate in discussions on GitHub issues. You can also contribute by submitting pull requests (PR) with code changes. Contributions are welcome. See information on [contributing](./CONTRIBUTING.md), as well as our [code of conduct](https://avanade.github.io/code-of-conduct/).

<br/>

## Security

See our [security disclosure](./SECURITY.md) policy.

<br/>

## Who is Avanade?

[Avanade](https://www.avanade.com) is the leading provider of innovative digital and cloud services, business solutions and design-led experiences on the Microsoft ecosystem, and the power behind the Accenture Microsoft Business Group.
