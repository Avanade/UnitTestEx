# Test set-up and extensibility

[← Back to README](../README.md)

<br/>

## `TestSetUp`

`TestSetUp.Default` holds the defaults that every new tester copies via `UseSetUp`. Change it once per test assembly. A tester can use a different set-up with `UseSetUp(customSetUp)`; `TestSetUp.Clone()` is a handy starting point.

Property | Default | Purpose
-|-|-
`DefaultUserName` | `"Anonymous"` | Used when `UseUser` / `WithUser` isn't called.
`UserNameConverter` | `null` | Converts the object passed to `UseUser(object)` into a user name.
`OnBeforeHttpRequestMessageSendAsync` | `null` | Runs before each API test request, and receives the user name. Use it to add auth headers or tokens, correlation IDs, etc.
`OnBeforeHttpRequestSendAsync` | `null` | The same for `HttpRequest`-based Function triggers.
`ConfigureServices` | `null` | Extra service configuration applied to **every** tester.
`JsonSerializer` / `JsonComparerOptions` | System.Text.Json | Serialization and JSON comparison defaults.
`MinimumLogLevel` | `Debug` | Lowest log level that gets captured.
`ConcurrencyErrorETag` | `"ZZZZZZZZZZZZ"` | Fake ETag for concurrency-error tests.
`Properties` | empty | Free-form bag for extensions.

Static settings: `TestSetUp.Environment` (host environment, default `Development`) and `TestSetUp.Extensions` (see below).

<br/>

## One-off assembly set-up

Mark the test assembly with `[assembly: OneOffTestSetUp(typeof(MySetUp))]`, where `MySetUp : OneOffTestSetUpBase`. UnitTestEx runs `SetUp()` **once** per assembly, automatically, when `TestSetUp` first initializes or the assembly loads. Use it to set `TestSetUp.Default` and register extensions. You can also call `OneOffTestSetUpAttribute.SetUp(assembly)` explicitly to force it early. That re-runs `SetUp()`, so keep your set-up idempotent.

Example: [`SetUp_SetDefaultUserName`](../tests/UnitTestEx.NUnit.Test/Other/OneOffTestSetUpTest.cs).

<br/>

## Repeatable set-up (e.g. database reset)

`TestSetUp.Default.RegisterSetUp(...)` or `RegisterAutoSetUp(...)` registers a function. Each `SetUp()` / `SetUpAsync()` call then runs it, thread-safely, and passes how many times it has run so far, so you can do "full reset on first run, cheap reset afterwards". `RegisterAutoSetUp` also fails the test automatically if set-up fails, and logs its output in the next test.

<br/>

## Tester extensions

`TesterExtensionsConfig` is the hook for **libraries** that build on UnitTestEx (CoreEx.UnitTesting uses it). Add your subclass to `TestSetUp.Extensions`. It can:

Override | Purpose
-|-
`OnUseSetUp(owner)` | React when a tester applies a `TestSetUp`.
`ConfigureServices(owner, services)` | Add services to **every** tester's host.
`UpdateValueFromHttpResponseMessage` / `UpdateValueFromActionResult` | Fill in a deserialized value from the response, e.g. copy an ETag header into the model.
`ExpectationAssertAsync(expectation, args)` | Add custom assertions to the expectations pipeline.

Example: [`TesterExtensions_ApiTester`](../tests/UnitTestEx.NUnit.Test/Other/OneOffTestSetUpTest.cs), [`TesterExtensions_FunctionTester`](../tests/UnitTestEx.NUnit.Test/Other/OneOffTestSetUpTest.cs), [`TesterExtensions_GenericTester`](../tests/UnitTestEx.NUnit.Test/Other/OneOffTestSetUpTest.cs).

<br/>

## Test framework support

Package | Base classes | Notes
-|-|-
`UnitTestEx.NUnit` | `WithApiTester<T>`, `WithGenericTester<T>` | Writes output to `TestContext`.
`UnitTestEx.MSTest` | `WithApiTester<T>`, `WithGenericTester<T>` | Writes output to `TestContext`.
`UnitTestEx.Xunit` | `UnitTestBase`, `ApiTestFixture<T>`, `WithApiTester<T>` | Pass `ITestOutputHelper` to the constructor. Inside a `UnitTestBase`, create testers as usual, e.g. `GenericTester.Create<T>()`, so output is routed to xUnit.

The test code is near-identical across frameworks. Compare [UnitTestEx.NUnit.Test](../tests/UnitTestEx.NUnit.Test), [UnitTestEx.MSTest.Test](../tests/UnitTestEx.MSTest.Test) and [UnitTestEx.Xunit.Test](../tests/UnitTestEx.Xunit.Test).

[← Back to README](../README.md)
