# Azure Functions testing

`FunctionTester` (package `UnitTestEx.Azure.Functions`) tests Azure Functions written for the **v4 isolated worker model** on .NET 8, 9 and 10. The in-process (WebJobs) model is not supported from v6 onwards; see [Migrating to v6](./migration-v6.md).

[← Back to README](../README.md)

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

<br/>

## Choosing a tester

Method | Use for | Trigger validation
-|-|-
`HttpTrigger<TFunction>()` | HTTP functions | Validates `[HttpTrigger]`, plus the HTTP method and route (configurable).
`ServiceBusTrigger<TFunction>()` | Service Bus functions | Validates `[ServiceBusTrigger]`.
`Trigger<TFunction>()` | Any other worker trigger, e.g. Event Grid, Blob, Queue, Timer, Event Hubs or Cosmos DB | Validates that the method has a worker `*TriggerAttribute`.
`Type<TFunction>()` | Any method, with no trigger checks | None.

All four support synchronous and asynchronous methods, instance and `static` methods, expectations (`Expect*`), log capture, and the standard assertors.

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
**Trigger metadata / raw binding data** | Middleware sees empty `BindingContext.BindingData` and trigger metadata, because the payload is passed as already-converted arguments. | [Aspire testing](./aspire.md).
**Input/output binding conversion** | Real binding converters, such as JSON-to-POCO, `[BlobInput]` and output bindings, aren't run. You supply the already-converted arguments, and outputs are returned values. | [Aspire testing](./aspire.md) with Azurite or emulators.
**Trigger delivery semantics** | Retries, poison queues, Service Bus auto-complete, Event Grid filtering and batching are runtime behaviour. | [Aspire testing](./aspire.md) with emulators.
**`host.json` / `local.settings.json`** | These are read by the Functions host, not your app's configuration. Use `appsettings.unittest.json` or `UseAdditionalConfiguration` for test values. | -
**Hosted services** | The host is never started, so `IHostedService` instances don't run. | Test them with `GenericTester`, or use Aspire.
**Code after `Build()` in `Program.cs`** | The host is captured at `Build()`. Code after it, such as `host.Run()`, never runs. | -

`FunctionContext`, where a function needs one, is a test `FunctionContext` backed by the test host's `IServiceProvider`. `HttpRequestData.FunctionContext.GetHttpContext()` is supported.

[← Back to README](../README.md)
