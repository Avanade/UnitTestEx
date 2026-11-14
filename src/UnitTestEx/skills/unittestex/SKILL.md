---
name: unittestex
description: Write or migrate UnitTestEx v7 ASP.NET Core API and Generic Host tests with dependency replacement, HttpClient mocking, fluent assertions, test-framework detection, output capture and shared fixtures. Use when a project references UnitTestEx or needs ApiTester, GenericTester or UnitTestBase.
license: MIT
---

# UnitTestEx v7 consumer guidance

## Choose the boundary first

Use `ApiTester` for ASP.NET Core's real in-process HTTP pipeline and `GenericTester` for DI-composed services. Use `UnitTestEx.Azure.Functions` for isolated-worker function invocation, `UnitTestEx.Aspire` for real multi-process applications, and the optional `UnitTestEx.Azure.Functions.Durable` preview for orchestration testing. Those packages carry their own focused skills.

Inspect the consumer's package versions, test framework and existing tests before editing. This skill describes v7, not earlier adapter-package APIs. Preserve the consumer's test framework and application structure; do not install extra packages or change target frameworks unless the task needs it.

## Framework and output

- Reference core `UnitTestEx` and the actual NUnit, MSTest or xUnit framework/runner packages. Do not add `UnitTestEx.NUnit`, `UnitTestEx.MSTest` or `UnitTestEx.Xunit`.
- Runtime detection supports xUnit v2/v3, NUnit v4/v5 and MSTest v3/v4. Optional explicit selection is `TestSetUp.TestFramework = TestFramework.Xunit3`, with `TestFramework` in `UnitTestEx.Abstractions`.
- For xUnit, pass the runner's helper to `Create<T>(output: output)`, or inherit `UnitTestEx.Abstractions.UnitTestBase` and call `base(output)`. The base supplies ambient per-test context; it does not own a tester. NUnit/MSTest normally use their current test context automatically.
- Explicit `createFactory` wins over ambient/global settings. Without one, the ambient implementor, local factory and global factory precede automatic detection. The `output` argument is used only when detection creates the implementor; existing implementors/factories own output routing.
- Missing/ambiguous automatic detection emits a diagnostic and uses framework-neutral assertions with Console output. Other frameworks can implement `TestFrameworkImplementor` and supply/register a factory.

## Start with the smallest test

```csharp
using UnitTestEx;

using var test = GenericTester.Create();
test.Run(() => 1).AssertSuccess().AssertValue(1);
```

For an API, use an existing accessible startup/entry-point type, then invoke a controller expression through HTTP or supply a route explicitly:

```csharp
using System.Net.Http;
using UnitTestEx;

using var test = ApiTester.Create<Startup>();
test.Http().Run(HttpMethod.Get, "Person/1").AssertOK();
```

`Startup` and the route are application-specific, not types/routes supplied by the library. For minimal hosting use an accessible application entry-point type supported by `WebApplicationFactory`; do not invent a separate startup just for the test. Match response assertions to the application's actual contract.

## Configure before building the host

Use `ConfigureServices`, `ReplaceSingleton`, `ReplaceScoped`, `MockSingleton`, `ReplaceHttpClientFactory` and configuration overrides before accessing services or running the test. `ResetHost()` rebuilds the host when needed. Use `UseSolutionRelativeContentRoot` when application files are outside the test output.

For a mocked downstream HTTP call:

```csharp
using System;
using System.Net.Http;
using UnitTestEx;

using var mock = MockHttpClientFactory.Create();
mock.CreateClient("catalog", new Uri("https://catalog.example"))
    .Request(HttpMethod.Get, "products/1")
    .Respond.WithJson(new { id = "1" });
using var test = GenericTester.Create();
test.ReplaceHttpClientFactory(mock);
```

Match the registered client name/base address and the actual outgoing request. Arrange mocks before invoking the application; verify expected calls with the mocking API described in the HTTP mocking guide.

`GenericTester.Run<TService>` resolves from the root provider. For scoped services use `ScopedType<TService>(scope => scope.Run(...).AssertValue(...))`. Do not resolve a scoped service from the root.

Use `AssertValue` for typed/structural values and `AssertJson` for JSON expectations. `Expect*` extensions require `using UnitTestEx.Expectations;`; they are arranged before `Run`. `Assert*` methods inspect the result afterwards. Use `ExpectLogContains`/`ExpectLogNotContains` for captured application logging.

## Ownership and fixtures

Dispose per-test testers with `using`. `WithApiTester<T>` and `WithGenericTester<T>` own testers and expose `Test`; configure through `OnConfiguration()` (once on first `Test` access) and clean up through `OnDispose()`.

For xUnit shared API fixtures, explicitly implement `IClassFixture<ApiTestFixture<T>>` on the test class and forward with `base(output: output, fixture: fixture)`. The neutral base does not implement xUnit interfaces. Never retain a per-test helper inside a shared fixture. Shared testers/data/logs require deliberate isolation; do not run the same mutable tester concurrently.

## Validate and consult only relevant references

Run the consumer's smallest affected test selection. Do not claim a mocked call proves real network or broker behavior.

- [API guide](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/api-testing.md)
- [Generic guide](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/generic-testing.md)
- [HTTP mocking](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/http-client-mocking.md)
- [DI and configuration](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/dependency-injection-and-configuration.md)
- [Frameworks and fixtures](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/test-setup-and-extensibility.md)
- [v5/v6/v7 migration](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/migration-v6.md)

Executable references: `tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs`, `Other/DependencyInjectionTest.cs`, `PersonControllerTest.cs` and `MockHttpClientTest.cs` in the same release source.
