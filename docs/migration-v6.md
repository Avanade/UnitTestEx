# Migrating from v5 to v6 to v7

This guide covers both breaking releases in one place. v6 drops end-of-life platforms and the Azure Functions in-process model. v7 consolidates test-framework integration and shared fixtures into core `UnitTestEx`, removing the separate framework adapter packages.

Upgrading from v5 directly to v7 requires both sets of changes. If you already use v6, focus on [test-framework integration](#test-framework-integration-v7) and the v7 checklist items. The existing document URL is retained for incoming links.

[← Back to README](../README.md)

<br/>

## Platform

v5 | v6 | v7
-|-|-
`net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` | `net8.0`, `net9.0`, `net10.0` | `net8.0`, `net9.0`, `net10.0`, `net11.0`

net6.0 and net7.0 are out of support, and their target-specific code paths have been removed.

<br/>

## Azure Functions: isolated worker only

Microsoft ends support for the in-process model on **10 November 2026**. v6 and v7 support only the **v4 isolated worker** model.

Removed | Replacement
-|-
In-process / WebJobs support, including the `FunctionsStartup` entry point and `Microsoft.Azure.WebJobs.*` dependencies | Isolated worker. `FunctionTester` now runs your app's **real** `Program.cs`.
`CreateWebJobsServiceBusMessageActions()` / `CreateWebJobsServiceBusSessionMessageActions()` and their WebJobs assertors | `CreateWorkerServiceBusMessageActions()` → `WorkerServiceBusMessageActionsAssertor`. Isolated-worker session message actions aren't supported yet.
`UnitTestEx.Azure.ServiceBus` package | The `CreateServiceBusMessage*` helpers now live in `UnitTestEx.Azure.Functions`, namespace `UnitTestEx`. Remove the package reference; your `using` stays the same.

### `TEntryPoint`

In v5, `FunctionTester.Create<TEntryPoint>()` needed a `Startup` class (`FunctionsStartup` or similar) with `new()`. In v6, `TEntryPoint` is **any type in the function app's assembly** and is used only to find the entry point. Pass a function class. You don't need a `Program` class, `BuildHost` method or other test-only structure: top-level statements in `Program.cs` work as-is.

``` csharp
// v5
using var test = FunctionTester.Create<Startup>();

// v6
using var test = FunctionTester.Create<ProductFunction>();
```

The `Program.cs` host is captured when `Build()` is called and is never started, so `host.Run()` doesn't block the test. See [How it works](./azure-functions.md#how-it-works).

### New in v6

- `HttpTrigger<T>()` works with **both** HTTP styles: ASP.NET Core integration (`HttpRequest` → `IActionResult`/`IResult`) and the built-in model (`HttpRequestData` → `HttpResponseData`). Both are asserted through the same `HttpResponseMessageAssertor`.
- `Trigger<T>()` runs any other trigger type (Timer, Blob, Queue, Event Hubs, Cosmos DB, ...) and gives multi-output assertions via `FunctionOutputAssertor`.
- Event Grid builders: `CreateEventGridEvent*` and `CreateCloudEvent*`.
- A test `FunctionContext`, backed by the test host's services, is available for functions that take a `FunctionContext` parameter.

See [Azure Functions testing](./azure-functions.md) for details, including the [Limitations](./azure-functions.md#limitations).

<br/>

## Checklist

1. Change test project `TargetFramework`s to `net8.0`+.
2. Migrate the function app to the isolated worker, following [Microsoft's guide](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model).
3. Replace `FunctionTester.Create<Startup>()` with `FunctionTester.Create<AnyFunctionClass>()`.
4. Remove the `UnitTestEx.Azure.ServiceBus` package reference.
5. Replace `CreateWebJobsServiceBus*MessageActions()` with `CreateWorkerServiceBusMessageActions()`.
6. For v7, remove `UnitTestEx.NUnit`, `UnitTestEx.MSTest`, or `UnitTestEx.Xunit` references and reference core `UnitTestEx` instead. Keep the actual NUnit, MSTest or xUnit packages and test adapters.
7. Update `UnitTestBase` imports, xUnit shared-fixture declarations and constructor calls as described below.
8. Use coordinated `7.0.0` UnitTestEx package versions; the optional Durable package uses `7.0.0-preview`.

<br/>

## Test-framework integration (v7)

Core `UnitTestEx` infers xUnit v2/v3, NUnit v4/v5, or MSTest v3/v4 at runtime. Separate UnitTestEx framework adapter packages are not required. Explicit selection uses the new `TestSetUp.TestFramework` enum property or the `UnitTestEx:TestFramework` configuration value. Other frameworks can integrate through a custom `TestFrameworkImplementor` factory.

API | v7 guidance
-|-
`UnitTestBase` | Add `using UnitTestEx.Abstractions;`. The base is framework-neutral and accepts an optional output object and implementor factory. Its protected `Output` property is `object?`, not xUnit's `ITestOutputHelper`; retain a typed helper in the derived class if you call xUnit-specific output APIs.
`WithApiTester<T>`, `WithGenericTester<T>`, `ApiTestFixture<T>` | These are in core `UnitTestEx`, with their `UnitTestEx` namespaces unchanged. Configuration runs once on first `Test` access through `OnConfiguration()`; use `OnDispose()` for cleanup.
Shared xUnit API fixtures | Declare `IClassFixture<ApiTestFixture<T>>` explicitly on the test class; the framework-neutral base does not implement xUnit interfaces. Forward constructor arguments with `base(output: output, fixture: fixture)` instead of the former `base(fixture, output)`.
Per-test output | Pass xUnit's output helper to a UnitTestEx base class or a direct tester's `output` parameter. NUnit and MSTest use their current test context automatically. Explicit implementor factories take precedence and must handle their own output.

For example, an xUnit v2 shared API fixture uses:

``` csharp
using UnitTestEx;
using Xunit;
using Xunit.Abstractions;

public class ApiTests(ApiTestFixture<Startup> fixture, ITestOutputHelper output)
    : WithApiTester<Startup>(output: output, fixture: fixture), IClassFixture<ApiTestFixture<Startup>>
{
}
```

Keep test-specific output helpers out of shared fixture constructors. See [test-framework support](./test-setup-and-extensibility.md#test-framework-support) for detection, fallback behavior and factory registration.

## Durable Functions preview (v7)

`UnitTestEx.Azure.Functions.Durable` is an optional `7.0.0-preview` package; ordinary Functions tests do not require it. It targets key testing scenarios rather than exhaustive Durable Functions coverage and evolves with usage and feedback. The SDK host remains preview and uses wall-clock timers; the separate method-level virtual-time context is not an orchestration engine. See the [Durable guide](./azure-durable-functions.md) and [support matrix](./azure-durable-functions.md#support-matrix) before choosing a test path.

[← Back to README](../README.md)
