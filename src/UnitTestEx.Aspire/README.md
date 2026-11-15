# UnitTestEx.Aspire

Real multi-process .NET Aspire integration testing with resource readiness, HTTP helpers, captured resource logs and external HTTP mocking. Package version **7.0.0**, with core UnitTestEx as a dependency.

## Quick start

```csharp
using System.Net.Http;
using UnitTestEx;

await using var tester = AspireTester.Create<Projects.MyAppHost>();
await tester.WaitForResourceAsync("shopping");
tester.Http("shopping").Run(HttpMethod.Get, "orders/123").AssertOK();
```

Reference your AppHost and use its generated `Projects.*` marker, resource name and route. The test target must be compatible with the AppHost assembly. Libraries target .NET 8/9/10/11; this repository's fixed AppHosts target .NET 10 and its integration suites target .NET 10/11.

Use `await using` for per-test ownership. Shared `WithAspireTester<T>` fixtures require asynchronous teardown and explicit state/log isolation. Use callback-supplied application objects inside startup hooks, not reentrant calls to the tester's own startup task.

For xUnit pass `output: output` or establish ambient `UnitTestBase` context. Unexpected resource error logs fail tests by default. External HTTP mocking uses a real WireMock resource, not an in-process handler in the test process.

## AI assistance and documentation

The package includes `skills/unittestex-aspire/SKILL.md`. See [discovery and optional installation](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/ai-support.md); installation into a consumer repository is explicit.

- [Aspire guide, lifecycle and mocking](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/aspire.md)
- [Compiled integration examples](https://github.com/Avanade/UnitTestEx/tree/v7.0.0/tests/UnitTestEx.Aspire.Xunit.Test)
- [Functions runtime versus invocation](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-functions.md#proof-boundary-invocation-versus-runtime)
