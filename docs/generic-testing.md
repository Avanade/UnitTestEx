# Generic testing

`GenericTester` (package `UnitTestEx`) runs any code inside a lightweight .NET Generic Host. You get UnitTestEx's DI, configuration, logging capture, expectations and assertions without needing an API or Function. Use it for domain services, repositories, message handlers and other plain classes.

[← Back to README](../README.md)

<br/>

## Create

``` csharp
using var test = GenericTester.Create();               // Empty host; only test configuration and logging.
using var test = GenericTester.Create<EntryPoint>();   // Host configured by an entry point class.
using var testWithOutput = GenericTester.Create<EntryPoint>(output: output); // Captures UnitTestEx output in the current test.
```

Pass a per-test output sink such as xUnit's `ITestOutputHelper`, an `Action<string>`, or a `TextWriter` when runner output capture is needed.
Pass `createFactory` to `GenericTester.Create`, `CreateFor`, or a direct tester constructor to provide a per-instance `TestFrameworkImplementor`; an explicit factory takes precedence over automatic framework detection and is responsible for handling its own output.

The entry point is any class that has **one or more** of the following methods. Each can be static or an instance method, and they're found by name using reflection, so no interface is needed:

Method | Purpose
-|-
`ConfigureApplication(IHostApplicationBuilder builder)` | Preferred; matches the modern `Host.CreateApplicationBuilder` style.
`ConfigureServices(IServiceCollection services)` | Service registrations.
`ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder config)` | App configuration.
`ConfigureHostConfiguration(IConfigurationBuilder config)` | Host configuration.

Example: [`Run_Service`](../tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs) with its `EntryPoint` class.

`new GenericTester<TEntryPoint, TValue>()` gives the tester a value type, so `ExpectValue` is strongly typed.

<br/>

## Run

`Run` accepts a function or action, either sync or async (`Task`, `ValueTask`). The `Run<TService>` / `Run<TService, TValue>` overloads resolve `TService` from the host for you:

``` csharp
test.Run(() => 1).AssertSuccess().AssertValue(1);
test.Run<Gin, int>(gin => gin.Pour()).AssertValue(1);
test.Run<Gin>(gin => gin.Stir()).AssertException<DivideByZeroException>("As required by Bond; shaken, not stirred.");
```

Example: [`Run_Success`](../tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs), [`Run_Exception`](../tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs), [`Run_Service`](../tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs).

> `Run<TService>` resolves from the **root** provider, so a **scoped** service throws `Cannot resolve scoped service from root provider`. Use `ScopedType<TService>` instead (see below).

<br/>

## `Type<T>` and `ScopedType<T>`

These are available on **every** tester (API, Function, Generic), so you can test any service registered in the host under test.

- `Type<TService>()` resolves the service from DI. If it isn't registered, it is constructed instead, with each constructor parameter resolved from DI. Then it runs methods on it.
- `ScopedType<TService>(t => ...)` creates a DI scope, resolves the service inside it, runs your test actions, then disposes the scope. Use this for scoped services, such as a `DbContext` or anything that depends on request-scoped state. `UseScopedTypeSetUp(sp => ...)` runs arrangement code inside every such scope first.

Example: [`ScopedType_RunsWithinServiceScope`](../tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs), [`Type_IActionResult`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Base classes

To reuse one tester across a test class, inherit from a framework-neutral base class. It creates the tester once and exposes it as `Test`:

Base class | Purpose
-|-
`WithApiTester<TEntryPoint>` | Reuses an API tester and can consume an optional `ApiTestFixture<TEntryPoint>`.
`WithGenericTester<TEntryPoint>` | Reuses a generic tester.
`UnitTestBase` | Provides per-test output/framework context when composing your own fixture.

`UnitTestBase` lives in `UnitTestEx.Abstractions`. The tester-owning bases call `OnConfiguration()` once, immediately before the first `Test` access, so overrides can configure the tester after the derived fixture has finished construction. Override `OnDispose()` for additional cleanup; the bases still dispose their owned tester if the hook throws. NUnit and MSTest can use these bases without extra constructor arguments. For xUnit, pass `ITestOutputHelper` as `output` so UnitTestEx output is captured by the test runner. Example: [`Run_Success_AssertJSON`](../tests/UnitTestEx.NUnit.Test/Other/WithGenericTesterTest.cs).

See also: [Dependency injection and configuration](./dependency-injection-and-configuration.md), [Assertions and expectations](./assertions-and-expectations.md).

[← Back to README](../README.md)
