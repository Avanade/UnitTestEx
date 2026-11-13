# Azure Functions testing

`FunctionTester` (package `UnitTestEx.Azure.Functions`) tests Azure Functions written for the **v4 isolated worker model** on .NET 8, 9, 10 and (from v6.2) 11. The in-process (WebJobs) model is not supported from v6 onwards; see [Migrating to v6](./migration-v6.md).

.NET 11 function apps require `Azure.Functions.Sdk` 1.0.1 or later as the project SDK, replacing `Microsoft.NET.Sdk` and the `Microsoft.Azure.Functions.Worker.Sdk` package reference. Keep an explicit `Microsoft.Azure.Functions.Worker` reference. The multi-targeted samples use `Azure.Functions.Sdk/1.0.2`; existing apps on older frameworks do not need to migrate just to use UnitTestEx. See [Microsoft's migration guidance](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide#migrate-to-azurefunctionssdk).

[← Back to README](../README.md)

<br/>

## Proof boundary: invocation versus runtime

**A passing `FunctionTester` test is not proof that the Functions runtime can index, bind or deliver the trigger, or render the final HTTP response.** Choose the tier for the claim:

Claim | Test with
-|-
Function logic, worker middleware, scoped DI, already-converted SDK arguments and explicit settlement calls | `FunctionTester`.
Trigger indexing, binding conversion, output bindings, actual HTTP rendering, broker retries/redelivery and Blob poison behavior | A running Functions host (Aspire/Core Tools) and its backing services or emulators.
Azure Event Grid subscription configuration, filtering and delivery/retry behavior | Azure Event Grid, not a manually posted webhook.

An Aspire test using Azurite and `FunctionsWebhook` runs the real Functions host against real emulator storage, but **simulates the notification delivery step**. It does not turn Azurite into Event Grid. Likewise, recorded Service Bus settlement calls prove what your function requested, not that a broker accepted those calls or redelivered a message.

The [runnable Blob integration example](#runnable-blob-integration-example) shows the runtime tier; the invocation examples below intentionally stop at the function boundary.

<br/>

## How it works

1. `FunctionTester.Create<TEntryPoint>()` runs the function app's **own** entry point (`Program.cs`, top-level statements and all). It does this through `HostFactoryResolver`, the same mechanism `WebApplicationFactory` uses. You don't need a `Program` class, a `BuildHost` method or any other test-only structure.
2. The host is captured at `Build()` and **never started**, so no worker gRPC channel, no Functions runtime and no hosted services run.
3. Before the host is built, test configuration is applied: `appsettings.unittest.json`, `UseAdditionalConfiguration`, and the environment set by `TestSetUp.Environment` (default `Development`). Service overrides (`Replace*`, `Mock*`, `ConfigureServices`, `ReplaceHttpClientFactory`) are applied too.
4. The function class is resolved from DI (or created with `ActivatorUtilities`). The method in your `Run` lambda is invoked through the app's **worker middleware pipeline**, so registered middleware runs before and after it (see [Worker middleware](#worker-middleware)).

`TEntryPoint` can be any type in the function app's assembly. It is only used to locate the assembly whose entry point is executed.

``` csharp
using var test = FunctionTester.Create<ProductFunction>();
```

Example: [`Program_TestOverrides`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Program_ServicesUsed_WorkerNotStarted`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Program_ConcurrentHostsAreIsolated`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs). The sample function app is [`UnitTestEx.IsolatedFunction`](../tests/UnitTestEx.IsolatedFunction/Program.cs).

Test configuration and service overrides are applied at the `Build()` boundary. Configuration values read eagerly, service-registration decisions made from those values, and other side effects executed earlier in `Program.cs` cannot be retroactively changed by this hook. `Program_TestOverrides` verifies the final configuration and replaced services, not arbitrary pre-`Build()` reads.

The worker reads `AZURE_FUNCTIONS_ENVIRONMENT` when its builder is created. `FunctionTester` temporarily sets this process-wide variable to `TestSetUp.Environment` during host construction and restores the previous value (including an absent value) before returning, even if construction fails. Host construction is serialized across all `FunctionTester` entry-point types. This does not isolate unrelated code that reads or writes the process environment during construction; serialize those tests too. A timed-out entry point cannot be forcibly stopped and may continue running after the environment is restored. `TestSetUp.Environment` is shared configuration: set it before starting tests, not concurrently during host construction.

<br/>

## Choosing a tester

Method | Use for | Trigger validation
-|-|-
`HttpTrigger<TFunction>()` | HTTP functions | Validates `[HttpTrigger]`, plus the HTTP method and route (configurable).
`ServiceBusTrigger<TFunction>()` | Service Bus functions | Validates `[ServiceBusTrigger]`.
`Trigger<TFunction>()` | Any other worker trigger, e.g. Event Grid, Blob, Queue, Timer, Event Hubs or Cosmos DB | Validates that the method has a worker `*TriggerAttribute`.
`Type<TFunction>()` | Any method, with no trigger checks | None.

All four support synchronous and asynchronous methods, instance and `static` methods, expectations (`Expect*`), log capture, and the standard assertors.

Scopes created by `HttpTrigger`, `ServiceBusTrigger`, and `Trigger` belong to the owning `FunctionTester`. They remain usable for subsequent invocations, response reading and assertions until that tester is reset or disposed. Reset/disposal releases these scopes before the host, including async-only scoped dependencies; individual trigger testers do not need to be disposed. Do not retain a trigger tester across an owner reset. Cleanup attempts all scopes and the host and reports disposal failures rather than silently ignoring them.

<br/>

## Durable activity invocation

`Trigger<TFunction>()` can invoke real `[ActivityTrigger]` methods using the existing Functions package. The [Durable sample](../tests/UnitTestEx.DurableFunction/Program.cs) uses normal application startup; [its activity tests](../tests/UnitTestEx.NUnit.Test/DurableActivityTest.cs) cover synchronous value/void, `Task`/`Task<T>`, DTO/collection inputs, DI, logging, and exceptions.

```csharp
using var test = FunctionTester.Create<WorkflowActivities>();
test.Trigger<WorkflowActivities>()
    .Run(_ => ActivityTransforms.NormalizeReference(" order-1 "))
    .AssertSuccess()
    .AssertValue("ORDER-1");

var trigger = test.Trigger<WorkflowActivities>();
var context = new TestFunctionContext(trigger.Services);
trigger.ExpectLogContains("Processing item A.")
    .Run(f => f.ProcessItem(new WorkItem("A", 3m), context))
    .AssertSuccess()
    .AssertValue(new ItemResult("A", 6m));
```

`ActivityTransforms` is a static class, so it cannot be a generic type argument. Use an ordinary application type as the marker and a static method call in the expression, as above; validation inspects the invoked method's trigger attribute. The sample types are in `UnitTestEx.DurableFunction`; `TestFunctionContext` is in `UnitTestEx.Azure.Functions`, and `ExpectLogContains` uses `UnitTestEx.Expectations`.

Direct activity tests do not start the app host or connect to a Durable backend. Activity failures remain their original exceptions rather than Durable failure metadata. This is **activity-method testing**, not orchestration execution or a Functions binding pipeline. For orchestration scheduling, replay and failure handling, use the optional `UnitTestEx.Azure.Functions.Durable` preview package. For the real worker and Azure Storage provider, use the separate opt-in Functions/Azurite integration suite. See the [Durable testing guide and showcase](./azure-durable-functions.md) and [support matrix](./azure-durable-functions.md#support-matrix).

Run the baseline for each supported framework, for example:

```powershell
dotnet test tests\UnitTestEx.NUnit.Test\UnitTestEx.NUnit.Test.csproj --framework net8.0 --filter FullyQualifiedName~DurableActivityTest
```

<br/>

## Fixture-level testing

`WithFunctionTester<TEntryPoint>` (in the `UnitTestEx` namespace, package `UnitTestEx.Azure.Functions`) exposes a `Test` property and implements `IDisposable` to release the tester, its trigger scopes and its host. The host is built lazily, so configure `Test` before accessing its services or invoking a function. There are no application-start lifecycle hooks because the worker is never started.

For an NUnit fixture that shares one tester across its test methods:

```csharp
[TestFixture]
public sealed class ProductFunctionTests : WithFunctionTester<ProductFunction>
{
    [Test]
    public void GetProduct()
    {
        Test.HttpTrigger<ProductFunction>()
            .Run(f => f.Run(Test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
            .AssertOK();
    }
}
```

The protected constructor accepts an optional `Func<TestFrameworkImplementor> createFactory`, like `WithAspireTester`. Passing `null` uses the normal framework resolution. An xUnit test class can pass its output helper explicitly:

```csharp
public sealed class ProductFunctionTests : WithFunctionTester<ProductFunction>
{
    public ProductFunctionTests(ITestOutputHelper output)
        : base(() => XunitTestImplementor.Create(output)) { }

    [Fact]
    public void GetProduct()
    {
        Test.HttpTrigger<ProductFunction>()
            .Run(f => f.Run(Test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
            .AssertOK();
    }
}
```

`XunitTestImplementor` is in `UnitTestEx.Xunit.Internal`; `ITestOutputHelper` is in `Xunit.Abstractions`. The explicit factory is also available through `FunctionTester.Create<TEntryPoint>(createFactory: ...)` and the concrete tester constructor; existing configuration arguments retain their order.

Sharing follows the framework's fixture lifetime. xUnit creates a test-class instance per test, so inheriting this base does **not** share the host across methods. Use an xUnit class or collection fixture for that lifetime; rebind its shared tester to the current test's output with `fixture.Test.ReplaceTestFrameworkImplementor(XunitTestImplementor.Create(output))` in the test-class constructor, rather than retaining one test's output helper. Ensure the owning fixture is disposed. Shared hosts and tester state can contaminate later tests; isolate mutable state, use `Test.ResetHost()` when needed, and do not use the shared tester concurrently.

Examples and lifecycle regressions: [`WithFunctionTesterTest` (NUnit)](../tests/UnitTestEx.NUnit.Test/WithFunctionTesterTest.cs), [`WithFunctionTesterTest` (xUnit)](../tests/UnitTestEx.Xunit.Test/WithFunctionTesterTest.cs).

<br/>

## HTTP triggers

Both HTTP programming models are supported:

Model | Request | Response
-|-|-
ASP.NET Core integration | `HttpRequest` (`test.CreateHttpRequest`, `test.CreateJsonHttpRequest`) | `IActionResult` / `IResult`
Worker-native | `HttpRequestData` (`test.CreateHttpRequestData`, `test.CreateJsonHttpRequestData`) | `HttpResponseData`

``` csharp
using var test = FunctionTester.Create<ProductFunction>();
test.ReplaceHttpClientFactory(mcf)
    .HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
    .AssertOK()
    .AssertValue(new { id = "Abc", description = "A blue carrot" });
```

Worker-native responses are converted to an `HttpResponseMessage`. This covers the status code, headers, cookies and body, so they can be checked with the full `HttpResponseMessageAssertor`:

``` csharp
test.HttpTrigger<HttpVariantFunctions>()
    .Run(f => f.DataJson(test.CreateJsonHttpRequestData(HttpMethod.Post, "data/json", new Person("Jane", "Doe"), null)))
    .ToHttpResponseMessageAssertor()
    .AssertOK()
    .AssertNamedHeader("X-Test", "abc")
    .AssertValue(new Person("Doe", "Jane"));
```

Scenario | Example
-|-
`HttpRequest` → `IActionResult` | [`Success`](../tests/UnitTestEx.NUnit.Test/ProductFunctionTest.cs), [`HttpTrigger_ActionResult_Sync`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
`IActionResult` → `HttpResponseMessage` | [`HttpTrigger_ActionResult_ToHttpResponseMessage`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
`HttpRequest` → `IResult` | [`HttpTrigger_Result_Async`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`HttpTrigger_Result_Sync`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
`HttpRequestData` → `HttpResponseData` (JSON) | [`HttpTrigger_RequestData_Json`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
`HttpRequestData` → `HttpResponseData` (text, route parameter) | [`HttpTrigger_RequestData_SyncText_RouteParameter`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
`HttpRequestData.FunctionContext.GetHttpContext()` | [`HttpTrigger_RequestData_GetHttpContext`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs)
JSON request body | [`ValidJson`](../tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs)
Assert against an embedded JSON resource | [`ValidJsonResource`](../tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs)
Validation errors (`AssertErrors`) | [`BadRequest2`](../tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs)
Unhandled exception | [`Exception`](../tests/UnitTestEx.NUnit.Test/ProductFunctionTest.cs)

### Method and route checks

By default `HttpTrigger` checks that the request's HTTP method is one the `[HttpTrigger]` allows, and that the request path matches its `Route`. This catches tests that call a function with a request it would never actually receive.

Method | Effect
-|-
`WithNoMethodCheck()` / `WithMethodCheck()` | Disables or enables the HTTP method check.
`WithNoRouteCheck()` | Disables the route check.
`WithRouteCheck(RouteCheckOption)` | Sets the route check: `Path`, `PathAndQuery`, `PathAndQueryStartsWith`, etc.

Example: [`HttpTrigger_RequestData_MethodCheck`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`HttpTrigger_RequestData_RouteCheck`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`WithPathAndQueryCheck`](../tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs), [`WithPathAndQueryStartsWithCheck`](../tests/UnitTestEx.NUnit.Test/PersonFunctionTest.cs).

### What HTTP response conversion executes

`ToHttpResponseMessageAssertor(request)` is **in-process result execution**, not a request sent through the Functions HTTP server:

Result | What happens
-|-
`IActionResult` | Calls `ExecuteResultAsync` with an `ActionContext` (empty route data and action descriptor).
`IResult` | Calls `ExecuteAsync` with the selected `HttpContext`.
`HttpResponseData` | Copies the already-written status, headers, cookies and body; it does not execute an ASP.NET Core result. This overload takes no request.
Multi-output `[HttpResult]` property | Uses the corresponding conversion above for the property's value.

For `IActionResult`/`IResult`, passing the **same request used for invocation** reuses its `HttpContext`: request headers (such as `Accept`), method, query, features, scoped `RequestServices`, and response headers already written by middleware remain available to the result executor. Without a request, conversion creates a fresh `DefaultHttpContext` using the tester's root services; it does not automatically recover the invocation request or its scope.

```csharp
var request = test.CreateHttpRequest(HttpMethod.Get, "action/sync");
test.HttpTrigger<HttpVariantFunctions>()
    .Run(function => function.ActionSync(request))
    .ToHttpResponseMessageAssertor(request)
    .AssertOK();
```

Conversion temporarily replaces `HttpContext.Response.Body`, executes the result synchronously into a fresh buffer, then copies the status, headers and rendered bytes into an `HttpResponseMessage`. The original response stream is restored in `finally`, including when execution throws, and is never disposed by conversion. Previously written bytes are **not** appended to the deferred result. Repeated conversion re-executes that result, so avoid doing it for results with side effects.

**Response-start/completion callbacks are not driven by this helper.** In the normal `DefaultHttpContext`, registering `Response.OnStarting` or `OnCompleted` does not cause the callback to run during conversion. Supplied custom response features or result code may implement their own behavior, but the helper does not simulate the server lifecycle. It also does not run the full Functions HTTP server pipeline, routing, authentication, transport or platform header handling. Keep real HTTP assertions in Aspire tests for those behaviors.

### Asserting an already-written HTTP response

If a function or adapter writes `HttpContext.Response` directly (or executes a result eagerly) and returns `EmptyResult`/`Results.Empty`, **snapshot the response instead of executing the returned result**:

```csharp
using var test = FunctionTester.Create<EagerResponseFunction>();
var request = test.CreateHttpRequest(HttpMethod.Post, "eager-response-proof");
using var body = new MemoryStream();
request.HttpContext.Response.Body = body;

var invocation = test.HttpTrigger<EagerResponseFunction>()
    .Run(function => function.Invoke(request));

invocation.SnapshotHttpResponse(request.HttpContext.Response)
    .AssertCreated()
    .AssertNamedHeader("X-Probe", "already-written")
    .AssertValue(new { value = "example" });
```

`SnapshotHttpResponse(HttpResponse)` is available on invocation assertors, including `ActionResultAssertor`, `HttpResultAssertor`, `ValueAssertor<T>` and `FunctionOutputAssertor<T>`. It first asserts invocation success, then copies status, headers and the **entire** buffered body from position zero. It does not invoke the function again, execute the returned result, run response callbacks, or guess rendering state from `EmptyResult`, status, headers or content type. Supplying the actual response is mandatory; a fresh context would not contain the written bytes.

The stream must remain readable and seekable. Set a `MemoryStream` before invocation, flush any pending `Response.BodyWriter` output before snapshotting, and keep the stream open until afterward. Snapshotting restores its position, never replaces/disposes it, and returns independent bytes and headers. Repeated snapshots do not duplicate output or re-execute rendering. Empty buffered responses and the default `Stream.Null` yield an empty body; unreadable, disposed or non-seekable streams produce an actionable `NotSupportedException`, not a silently empty response.

`ToHttpResponseMessageAssertor(request)` retains its deferred-execution semantics: an `EmptyResult` writes no bytes, even if the original response buffer contains a body. Use the snapshot path for eager responses; ordinary deferred `ObjectResult`, `ContentResult` and `IResult` tests continue to use conversion.

Examples and ownership regressions: [`HttpRenderingTest`](../tests/UnitTestEx.NUnit.Test/Other/HttpRenderingTest.cs). Real Core Tools/Aspire HTTP tests remain necessary to prove the final wire response.

### Multi-output functions

A function can return a POCO containing an `[HttpResult]` plus other output bindings, such as `[ServiceBusOutput]`. `Output(x => x.Property)` asserts a non-HTTP output, and `ToHttpResponseMessageAssertor()` asserts the HTTP result:

``` csharp
test.HttpTrigger<HttpVariantFunctions>()
    .Run(f => f.MultiOutput(test.CreateHttpRequestData(HttpMethod.Get, "multi-output")))
    .Output(x => x.Message)
    .AssertValue("multi-output message");
```

Example: [`HttpTrigger_MultiOutput`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs).

<br/>

## Service Bus triggers

`test.CreateServiceBusMessageFromValue`, `...FromJson`, `...FromResource` and `CreateServiceBusMessage` create a `ServiceBusReceivedMessage` that looks as if Azure Service Bus delivered it, with a lock token, delivery count, enqueued time and so on.

``` csharp
using var test = FunctionTester.Create<ServiceBusFunction>();
test.ReplaceHttpClientFactory(mcf)
    .ServiceBusTrigger<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" })))
    .AssertSuccess();
```

Functions that settle messages themselves can be given `test.CreateWorkerServiceBusMessageActions()`. It records what the function did, which can then be asserted with `AssertComplete`, `AssertAbandon`, `AssertDeadLetter`, `AssertDefer` or `AssertNone`:

``` csharp
var msg = test.CreateServiceBusMessageFromValue(new Person { FirstName = null, LastName = "Smith" });
var actions = test.CreateWorkerServiceBusMessageActions();

test.ServiceBusTrigger<ServiceBusFunction>()
    .Run(f => f.Run3(msg, actions))
    .AssertSuccess();

actions.AssertDeadLetter("Validation error", "First name is required");
```

Scenario | Example
-|-
Message from a value | [`ServiceBusMessage_Success`](../tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)
POCO-bound trigger | [`Object_Success`](../tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)
Message settlement (`ServiceBusMessageActions`) | [`ServiceBusMessage_AssertActions`](../tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)
Downstream HTTP failure | [`ServiceBusMessage_HttpClientError`](../tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)
Exception assertion | [`ServiceBusMessage_ThrowsException`](../tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs)

<br/>

## Event Grid triggers

`CreateEventGridEventFromValue/FromJson/FromResource` and `CreateCloudEventFromValue/FromJson/FromResource` build an `EventGridEvent` or a `CloudEvent` (JSON data) to pass to an Event Grid-triggered function through `Trigger<TFunction>()`:

``` csharp
var eventGridEvent = test.CreateEventGridEventFromValue(new Person("Jane", "Doe"), "/people/1", "People.Created");
var cloudEvent = test.CreateCloudEventFromResource<MyResources>("person-created.json", "/people", "People.Created");

test.Trigger<MyEventGridFunction>()
    .Run(f => f.Run(eventGridEvent))
    .AssertSuccess();
```

These helpers only create events. `EventGridExtensionMethods.CreateBlobCreatedEvent(...)` also builds the Azure Storage `Microsoft.Storage.BlobCreated` event shape from a container/blob name, URI and properties. It does not emulate delivery, retries or filtering. With Aspire and Azurite, upload the blob to the emulator and post the event yourself through `tester.FunctionsWebhook("functions").PostBlobCreatedAsync(...)`: the Functions host then handles the webhook, binding, invocation, and output, but Azurite does not publish or deliver Event Grid events. This verifies the function/runtime path, not Event Grid subscription configuration or delivery semantics. Test those separately against Azure Event Grid or a suitable emulator. Use `EventuallyAsync(...)` to wait for the function's external side effect. See [Aspire testing](./aspire.md#driving-azure-functions-triggers-and-waiting-for-background-work).

Example: [`EventGridEvent_FromValue`](../tests/UnitTestEx.NUnit.Test/EventGridExtensionMethodsTest.cs), [`CloudEvent_FromResource`](../tests/UnitTestEx.NUnit.Test/EventGridExtensionMethodsTest.cs).

<br/>

## Event Hubs triggers

`CreateEventDataFromValue/FromJson/FromResource` build received `EventData` values using the test's JSON serializer or an embedded JSON resource. `CreateEventData` accepts raw `BinaryData` and supports setting partition key, sequence number, offset, enqueue time and custom event properties:

```csharp
var eventData = test.CreateEventDataFromValue(
    new Person("Jane", "Doe"),
    message => message.Properties["source"] = "unit-test");

test.Trigger<CustomerEvents>()
    .Run(function => function.Process(eventData))
    .AssertSuccess();
```

Batch-trigger functions accept an `EventData[]` directly. These helpers exercise the function and UnitTestEx expectations, middleware and log capture; they do not emulate Event Hubs delivery, partition assignment, checkpointing or retries. Use an Event Hubs emulator or Azure for those integration behaviors.

Example: [`EventHubsTest`](../tests/UnitTestEx.NUnit.Test/EventHubsTest.cs).

<br/>

## Other triggers

For triggers without a dedicated helper, use `Trigger<TFunction>()`, or `Type<TFunction>()` to skip trigger validation. Pass the trigger payload directly: a `string`, POCO, `BinaryData`, `TimerInfo` and so on.

Blob and Storage Queue triggers use this same generic tester; no trigger-specific UnitTestEx tester or message builder is needed. The function app still references the corresponding isolated-worker storage extension package, which supplies the trigger attributes. Pass the value the function receives after Azure's binding conversion:

``` csharp
var content = BinaryData.FromString("""{"firstName":"Jane","lastName":"Doe"}""");

test.Trigger<StorageTriggerFunctions>()
    .Run(f => StorageTriggerFunctions.ProcessBlob(content, "people.json"))
    .AssertValue("""people.json:{"firstName":"Jane","lastName":"Doe"}""");

test.Trigger<StorageTriggerFunctions>()
    .Run(f => StorageTriggerFunctions.ProcessQueuePerson(new Person("Jane", "Doe")))
    .AssertValue("Jane Doe");
```

These are function-invocation unit tests: they do not test Azure's blob/queue binding conversion, storage connectivity, or delivery behavior. Use Aspire with Azurite for those integration scenarios.

``` csharp
test.Trigger<ServiceBusFunction>()
    .Run(f => ServiceBusFunction.Run4(new Person("Jane", "Doe"))) // static methods are supported.
    .AssertSuccess();

test.Type<ProductFunction>()
    .ExpectLogContains("(DI)")
    .Run(f => f.DailyRun(default!)) // Timer trigger.
    .AssertSuccess();
```

Example: [`BlobTrigger_Bytes`](../tests/UnitTestEx.NUnit.Test/StorageTriggerTest.cs), [`QueueTrigger_Person`](../tests/UnitTestEx.NUnit.Test/StorageTriggerTest.cs), [`QueueTrigger_String`](../tests/UnitTestEx.NUnit.Test/StorageTriggerTest.cs), [`GenericTrigger_ServiceBus`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`TimerTriggered`](../tests/UnitTestEx.NUnit.Test/ProductFunctionTest.cs).

<br/>

## Worker middleware

Middleware registered in `Program.cs` (`builder.UseMiddleware<T>()`, `UseWhen`, inline `Use`) runs around the function method, **in the same order as in production**. This includes the ASP.NET Core integration middleware from `ConfigureFunctionsWebApplication()`. No test-specific setup is needed. This covers:

- **Logging**: middleware logs are captured, so `ExpectLogContains` works.
- **Short-circuiting**: when middleware returns without calling `next`, for example to deny access, the function method isn't invoked and the response is what you assert on. This works for `HttpResponseData` (set via `GetInvocationResult().Value`) and for `HttpRequest`/`IActionResult` (written to `HttpContext.Response`).
- **Exception mapping**: when middleware catches an exception and writes a response, that response is what you assert on. If middleware doesn't handle the exception, it surfaces as normal (`AssertException<T>`).
- **Context**: `context.FunctionDefinition.Name`, `Parameters`, `InputBindings` and `OutputBindings` are derived from the method's `[Function]` and binding attributes. `GetHttpRequestDataAsync()`, `GetHttpContext()` and `GetInvocationResult()` all work.

``` csharp
var req = test.CreateHttpRequest(HttpMethod.Get, "action/sync", r => r.Headers["X-Middleware"] = "deny");
test.HttpTrigger<HttpVariantFunctions>()
    .Run(f => f.ActionSync(req)) // middleware short-circuits; ActionSync is never invoked.
    .ToHttpResponseMessageAssertor()
    .AssertUnauthorized();
```

To invoke the method directly and bypass middleware, use `FunctionTester.Create<T>().UseWorkerMiddleware(false)`.

Example: [`Middleware_Logs_Captured_Http`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_Logs_Captured_NonHttp`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_ShortCircuit_ActionResult`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_ShortCircuit_HttpResponseData`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_HandlesException_ActionResult`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_UnhandledException_Propagates`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs), [`Middleware_Bypassed`](../tests/UnitTestEx.NUnit.Test/IsolatedFunctionTest.cs). The sample middleware is [`TestMiddleware`](../tests/UnitTestEx.IsolatedFunction/TestMiddleware.cs).

> The pipeline integration implements some internal worker contracts (`IFunctionBindingsFeature`, and the ASP.NET Core integration's `IHttpCoordinator`) using `DispatchProxy`. It is verified against Worker.Core 1.19+ and Http.AspNetCore 2.x. Middleware that depends on trigger metadata or raw input binding data (`BindingContext.BindingData`, `BindInputAsync` for non-HTTP bindings) sees empty values.

<br/>

## Limitations

`FunctionTester` invokes your function method through the worker middleware pipeline inside a real, built (but not started) DI host. That keeps tests fast and deterministic, but some parts of a real Functions invocation are skipped:

Not exercised | Why it matters | Where to test it instead
-|-|-
**Trigger indexing** | Checking an attribute is not Functions host discovery/indexing or listener startup. | `WaitForFunctionsAsync` plus a real delivery test in [Aspire](./aspire.md).
**Final runtime HTTP behavior** | Result execution against a test context does not drive the server lifecycle or response-start callbacks. | A real HTTP request to the Aspire-hosted Functions app.
**Trigger metadata / raw binding data** | Middleware sees empty `BindingContext.BindingData` and trigger metadata, because the payload is passed as already-converted arguments. | [Aspire testing](./aspire.md).
**Input/output binding conversion** | Real binding converters, such as JSON-to-POCO, `[BlobInput]` and output bindings, aren't run. You supply the already-converted arguments, and outputs are returned values. | [Aspire testing](./aspire.md) with Azurite or emulators.
**Trigger delivery semantics** | Retries, poison queues, Service Bus auto-complete, Event Grid filtering and batching are runtime behaviour. | [Aspire testing](./aspire.md) with emulators.
**`host.json` / `local.settings.json`** | These are read by the Functions host, not your app's configuration. Use `appsettings.unittest.json` or `UseAdditionalConfiguration` for test values. | -
**Hosted services** | The host is never started, so `IHostedService` instances don't run. | Test them with `GenericTester`, or use Aspire.
**Code after `Build()` in `Program.cs`** | The host is captured at `Build()`. Code after it, such as `host.Run()`, never runs. | -

`FunctionContext`, where a function needs one, is a test `FunctionContext` backed by the test host's `IServiceProvider`. `HttpRequestData.FunctionContext.GetHttpContext()` is supported.

## Runnable Blob integration example

Use the existing checked-in example rather than inventing a test-only `Program` or a second infrastructure setup:

Project | References and purpose
-|-
[`UnitTestEx.IsolatedFunction`](../tests/UnitTestEx.IsolatedFunction/UnitTestEx.IsolatedFunction.csproj) | Worker SDK, `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs`; [`StorageTriggerFunctions`](../tests/UnitTestEx.IsolatedFunction/StorageTriggerFunctions.cs) reads `unittestex` with `Source = BlobTriggerSource.EventGrid` and writes `processed/result.txt`.
[`UnitTestEx.Aspire.Functions.AppHost`](../tests/UnitTestEx.Aspire.Functions.AppHost/UnitTestEx.Aspire.Functions.AppHost.csproj) | `Aspire.Hosting.AppHost` and the function project; [`AppHost.cs`](../tests/UnitTestEx.Aspire.Functions.AppHost/AppHost.cs) provisions Azurite and starts Core Tools against the built function output, restricted to `BlobTrigger`.
[`UnitTestEx.Aspire.NUnit.Test`](../tests/UnitTestEx.Aspire.NUnit.Test/UnitTestEx.Aspire.NUnit.Test.csproj) | `UnitTestEx.Aspire`, **`UnitTestEx.Azure.Functions`**, `UnitTestEx.NUnit`, `Azure.Storage.Blobs` and the AppHost project.

Both helpers below use the **`UnitTestEx` namespace**, but they ship in different packages:

```csharp
using UnitTestEx;                // Both helpers; reference both packages.
using Aspire.Hosting.Testing;    // app.GetEndpoint(...)
using Azure.Storage;
using Azure.Storage.Blobs;
using System.Text;

// After uploading inputBlob and creating the output container:
var createdEvent = EventGridExtensionMethods.CreateBlobCreatedEvent( // UnitTestEx.Azure.Functions
    "unittestex", "customers.csv", inputBlob.Uri,
    upload.Value.ETag.ToString(), "text/csv", Encoding.UTF8.GetByteCount(content));

using var response = await tester.FunctionsWebhook("functions")     // UnitTestEx.Aspire
    .PostBlobCreatedAsync("Host.Functions.BlobTrigger", createdEvent);
response.EnsureSuccessStatusCode();
```

The complete [`AzuriteBlobTriggerTest`](../tests/UnitTestEx.Aspire.NUnit.Test/Functions/AzuriteBlobTriggerTest.cs) includes all imports, emulator credentials, storage endpoint resolution, container provisioning, upload, notification delivery and output polling. It first calls `WaitForFunctionsAsync("functions", ["BlobTrigger"])`, then uses the upload's actual ETag and UTF-8 byte length. It handles only an expected storage 404 while waiting; other storage errors fail the test.

From the repository root on Windows, with .NET SDKs for the repository's targets, an Aspire-compatible container runtime (Docker or Podman), and Azure Functions Core Tools v4 on `PATH`:

```powershell
dotnet test tests\UnitTestEx.Aspire.NUnit.Test\UnitTestEx.Aspire.NUnit.Test.csproj --framework net10.0 --filter FullyQualifiedName~AzuriteBlobTriggerTest
```

Keep ports `7071` and `10000`-`10002` free and run fixed-port emulator fixtures/frameworks sequentially. The AppHost starts and disposes its resources; the test does not require a separately running `func start`.

For JSON report blobs, deserialize with options matching the producer's contract, for example `JsonSerializer.Deserialize<Report>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))` for a web-default producer. UnitTestEx's assertion serializer does not configure independent storage SDK or `System.Text.Json` calls. For Blob poison inspection, configure `QueueClientOptions.MessageEncoding = QueueMessageEncoding.Base64` when the host writes Base64 queue messages (or decode explicitly once). Do not deserialize encoded text or decode a message twice. The sample above verifies text output; it does not claim poison/retry coverage.

[← Back to README](../README.md)
