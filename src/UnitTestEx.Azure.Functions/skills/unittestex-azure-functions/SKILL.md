---
name: unittestex-azure-functions
description: Write UnitTestEx v7 Azure Functions v4 isolated-worker tests using normal Program.cs startup, HttpTrigger, ServiceBusTrigger, Trigger, dependency overrides and worker middleware. Use for function invocation testing or choosing between invocation tests and real Functions runtime integration.
license: MIT
---

# Azure Functions invocation testing

Use package `UnitTestEx.Azure.Functions` v7 with an existing test framework/runner. It depends on core UnitTestEx; consult the `unittestex` skill for shared DI, assertions, output and fixture conventions. Do not add the retired UnitTestEx framework adapter packages.

## Preserve the application's startup

`FunctionTester.Create<TEntryPoint>()` accepts any ordinary class in the function application's assembly. It executes that assembly's real entry point, captures the host at `Build()` and does not start it. Top-level `Program.cs` is supported: do not require a custom `Program`, `BuildHost`, `FunctionsStartup` or other test-only application structure.

Configure replacements before the first invocation/service access. Test overrides apply at `Build()`; they cannot retroactively change configuration decisions or side effects earlier in `Program.cs`. Hosted services, the worker channel and Functions runtime are not started.

## Choose the invocation API

| API | Use |
|---|---|
| `HttpTrigger<TFunction>()` | HTTP invocation, with worker HTTP method/route validation. |
| `ServiceBusTrigger<TFunction>()` | Service Bus invocation and recorded settlement calls. |
| `Trigger<TFunction>()` | Other worker triggers and direct activity-method invocation. |
| `Type<TFunction>()` | Ordinary method testing without trigger validation. |

Use `using UnitTestEx;` for the factory, `UnitTestEx.Azure.Functions` for function helpers such as `TestFunctionContext`, and `UnitTestEx.Expectations` for `Expect*` extensions. For HTTP:

```csharp
using System;
using System.Net.Http;
using UnitTestEx;

using var mock = MockHttpClientFactory.Create();
mock.CreateClient("XXX", new Uri("https://d365test"))
    .Request(HttpMethod.Get, "products/abc")
    .Respond.WithJson(new { id = "Abc", description = "A blue carrot" });
using var test = FunctionTester.Create<ProductFunction>();
test.ReplaceHttpClientFactory(mock);
test.HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "product/abc"), "abc"))
    .AssertOK();
```

`ProductFunction`, its `Run` signature, client name/base address and route are application-specific; this shape is exercised by the repository's Functions examples. Use the consumer's actual trigger method and already-converted arguments.

HTTP invocation supports ASP.NET Core `HttpRequest` with `IActionResult`/`IResult`, and built-in `HttpRequestData` with `HttpResponseData`. Registered worker middleware runs by default; `UseWorkerMiddleware(false)` opts out. For already-written `HttpContext.Response`, use the invocation assertor's `SnapshotHttpResponse` rather than executing the returned result a second time.

## Output, lifetime and assertions

For xUnit use `FunctionTester.Create<ProductFunction>(output: output)` or an ambient `UnitTestBase(output)`. Explicit `createFactory` implementations own output handling. `WithFunctionTester<T>` owns a tester and supplies lazy `OnConfiguration()` and cleanup `OnDispose()` hooks.

Dispose the owner with `using`. Trigger scopes belong to the tester until reset/disposal; do not dispose individual trigger testers or retain them across owner resets. Avoid concurrent tests that mutate process-wide configuration/environment.

Use `Replace*`/`Mock*`, `ConfigureServices`, `UseAdditionalConfiguration` and `ReplaceHttpClientFactory` before host creation. Assert the real application result and relevant log/settlement calls, not implementation details of the mock.

## Do not overstate what passed

- This is isolated-worker invocation testing, not in-process/WebJobs support.
- It does not prove trigger indexing, binding conversion, output-binding delivery, broker acceptance/redelivery or final runtime HTTP behavior.
- Use a running Functions host (Aspire/Core Tools) plus the backend/emulator for runtime claims. Posting a BlobCreated webhook simulates notification delivery; it is not a real Event Grid subscription.
- Direct activity invocation tests the activity body, not orchestration scheduling/replay. Use the optional `UnitTestEx.Azure.Functions.Durable` preview for that boundary.

Run the consumer's targeted tests after edits. Do not change their application startup solely to fit an example.

- [Functions guide and boundaries](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-functions.md)
- [Durable guide](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md)
- [Compiled Functions examples](https://github.com/Avanade/UnitTestEx/tree/v7.0.0/tests/UnitTestEx.NUnit.Test)

Executable references: `ProductFunctionTest.cs`, `IsolatedFunctionTest.cs` and `DurableActivityTest.cs` in that test project.
