# UnitTestEx.Azure.Functions

Azure Functions v4 isolated-worker invocation testing on .NET 8/9/10/11. Package version **7.0.0**; core UnitTestEx is a dependency.

## Quick start

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

This uses the repository's `ProductFunction` sample and named downstream HTTP client. Use your application's function type, signature, client details and route. The tester executes the normal application entry point and captures the host at `Build()` without starting the worker/runtime. No test-only `Program` or `BuildHost` method is required. Configure DI/mocks before the first invocation.

`HttpTrigger`, `ServiceBusTrigger` and `Trigger` validate worker trigger attributes and run the worker middleware pipeline. A passing invocation does not prove runtime indexing, binding conversion, broker delivery or final HTTP rendering. Use Aspire/Core Tools with a backend for those claims. Dispose the tester to release its trigger scopes and host.

For xUnit pass `output: output` to `FunctionTester.Create<T>()`, or establish ambient `UnitTestBase` context. Keep your framework/runner packages, not the retired UnitTestEx framework adapters.

## AI assistance and documentation

The package includes `skills/unittestex-azure-functions/SKILL.md`. Restore delivers it to the NuGet cache, not automatically into an agent's active skills. See [discovery and optional installation](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/ai-support.md).

- [Functions guide and proof boundaries](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-functions.md)
- [Compiled invocation examples](https://github.com/Avanade/UnitTestEx/tree/v7.0.0/tests/UnitTestEx.NUnit.Test)
- [Optional Durable orchestration testing](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-durable-functions.md)
