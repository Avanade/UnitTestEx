---
name: unittestex-aspire
description: Write UnitTestEx v7 Aspire multi-process integration tests using AspireTester, resource readiness, real HTTP, WireMock resources, captured resource logs and async fixture disposal. Use when testing an Aspire AppHost or diagnosing resource lifecycle and integration-test isolation.
license: MIT
---

# Aspire integration testing

Use `UnitTestEx.Aspire` v7 when the test must exercise real separately hosted resources over the network. For one API/service in-process prefer core `ApiTester`/`GenericTester`; for direct isolated-worker invocation prefer `FunctionTester`.

The package depends on core UnitTestEx. Consult the `unittestex` skill for shared framework/output and assertion conventions; do not add retired UnitTestEx framework adapter packages.

## Use the existing AppHost and wait for readiness

Reference the AppHost project and use the generated `Projects.*` marker, not the web API's `Program` or an invented marker. The test target must be compatible with the referenced AppHost. UnitTestEx libraries support .NET 8/9/10/11; this repository's fixed AppHosts target .NET 10 and integration suites target .NET 10/11.

```csharp
using System.Net.Http;
using UnitTestEx;

await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>()
    .WithResourceEnvironment("api", "SpecialKey", "VerySpecialValue");
await tester.WaitForResourceAsync("api");
tester.Http("api")
    .Run(HttpMethod.Get, "Person?firstName=John&lastName=Doe")
    .AssertOK()
    .AssertContent("John-Doe-");
```

This is the repository's `AspireTesterTest.Http_Get_ReturnsSuccess` workflow. Replace the AppHost marker, resource name, environment values and route with the consumer's definitions.

Use bounded readiness/polling against actual resource conditions, not arbitrary sleeps. Do not start a second infrastructure stack if the consumer already supplies an AppHost.

## Lifecycle and isolation

- Use `await using` for a per-test tester. `WithAspireTester<TAppHost>` shares a tester for the framework fixture lifetime; arrange asynchronous fixture teardown to await its disposal.
- Configure before the first operation starts the host. `BeforeStart` callbacks run after build but before start; `AfterStart` callbacks run after resources have been started, not necessarily become healthy.
- Inside either callback use its supplied `DistributedApplication`. Do not await the tester's own `GetDistributedApplicationAsync()` or instance readiness methods: startup is already in progress and can deadlock.
- Before start, use `AspireTesterBase.GetConnectionStringAsync(app, name)` for model connection strings; Aspire testing endpoint/client APIs require startup.
- Shared fixtures retain external state and captured logs. Reset application/backend state and call `ResetLogs()` when appropriate. Do not concurrently share a mutable tester.
- When diagnosing local contention, run integration target frameworks sequentially. CI runs targets on independent runners; do not infer a code defect solely from local host timing variance.

## Output, logs and downstream HTTP

For xUnit supply `Create<TAppHost>(output: output)` or use ambient `UnitTestBase(output)` context. Shared `WithAspireTester` fixtures do not retain test-specific output helpers.

`ErrorWhenLogContains` continuously checks drained resource logs and defaults to `LogLevel.Error`. `AssertLogContains`/`AssertLogNotContains` inspect captured logs. `Checkpoint("reason")` flushes pending output without sleeping; `ResetLogs()` discards captured history, not error-check configuration.

Aspire HTTP mocks need a real WireMock resource declared by the AppHost. Use the documented `HttpMock`/`AspireHttpMockClient` API against that resource. Do not substitute core in-process `MockHttpClientFactory` and claim it intercepts another process's requests. Configure consumer resource endpoints accordingly.

WireMock does not read GET/HEAD bodies by default; do not rely on body matchers for those requests. Do not disable error detection merely to make a failing integration test green; scope exclusions to known expected noise.

Run only the relevant integration selection, check prerequisites and report missing infrastructure explicitly. A passing local-emulator test does not establish deployed cloud authentication, scale or delivery guarantees.

- [Aspire guide, hooks and HTTP mocking](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/aspire.md)
- [Compiled xUnit integration examples](https://github.com/Avanade/UnitTestEx/tree/v7.0.0/tests/UnitTestEx.Aspire.Xunit.Test)
- [Functions runtime boundary](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/azure-functions.md#proof-boundary-invocation-versus-runtime)
