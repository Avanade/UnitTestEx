# Migrating from v5 to v6

v6 is a **breaking** release. It drops end-of-life platforms and the Azure Functions in-process model. Most API and generic tests compile unchanged. The work is mostly in Azure Functions test projects.

[← Back to README](../README.md)

<br/>

## Platform

v5 | v6
-|-
`net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` | `net8.0`, `net9.0`, `net10.0` only

net6.0 and net7.0 are out of support, and their target-specific code paths have been removed.

<br/>

## Azure Functions: isolated worker only

Microsoft ends support for the in-process model on **10 November 2026**. v6 supports only the **v4 isolated worker** model.

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

[← Back to README](../README.md)
