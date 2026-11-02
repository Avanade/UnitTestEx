# Dependency injection and configuration

Every tester (API, Function, Generic) runs the **real** host composition from your app, then lets you override parts of it. Test configuration and service overrides are layered **on top of** the app's own, so the override always wins.

[← Back to README](../README.md)

<br/>

## Replacing services

All `Replace*` and `Mock*` methods remove any existing registration for the service type, then register the replacement.

Method | Description
-|-
`ReplaceSingleton` / `ReplaceScoped` / `ReplaceTransient` | Replace with an instance, a factory, a type, or a `<TService, TImplementation>` pair.
`ReplaceKeyedSingleton` / `ReplaceKeyedScoped` / `ReplaceKeyedTransient` | The same for keyed services.
`MockSingleton` / `MockScoped` / `MockTransient` | Register a Moq `Mock<T>` and resolve its `.Object`.
`ReplaceHttpClientFactory(mcf)` | Swap `IHttpClientFactory` for a [`MockHttpClientFactory`](./http-client-mocking.md).
`ConfigureServices(sc => ...)` | Any other `IServiceCollection` change. The extension methods `sc.ReplaceSingleton(...)` etc. work here too.

``` csharp
var mock = new Mock<IGreeter>();
mock.Setup(x => x.Greet()).Returns("mocked");

using var test = GenericTester.Create().ConfigureServices(sc => sc.AddSingleton<IGreeter, RealGreeter>());
test.MockSingleton(mock)
    .Run<IGreeter, string>(g => g.Greet())
    .AssertValue("mocked");
```

Example: [`ReplaceSingleton_OverridesRegistration`](../tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs), [`ReplaceScoped_OverridesRegistration`](../tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs), [`MockSingleton_UsesMoqObject`](../tests/UnitTestEx.NUnit.Test/Other/DependencyInjectionTest.cs).

<br/>

## Host lifetime

The host is built lazily, the first time `Services`, `Configuration` or a `Run` needs it, and is then reused across runs on the same tester. Each `Replace*` / `Mock*` / `ConfigureServices` call has an `autoResetHost` parameter (default `true`), so the next run rebuilds the host with the change applied.

- `ResetHost()` forces a rebuild; `ResetHost(resetConfiguredServices: true)` also throws away the registered overrides. Example: [`Get_Test4_WithResetHost`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).
- `OnHostStart(() => ...)` registers logic that runs each time the host is (re)built.
- `Services` is the built `IServiceProvider`. Use it to arrange or inspect state directly. Example: [`ServiceProvider`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

<br/>

## Configuration

Configuration sources, layered on top of the app's own:

Source | Notes
-|-
`appsettings.unittest.json` | In the **test** project, copied to output. Loaded automatically, so you can change settings for tests without touching the app under test.
Environment variables | Standard host behaviour.
`UseAdditionalConfiguration(key, value)` / `UseAdditionalConfiguration(pairs)` | Per-tester overrides; these take precedence.
`TestSetUp.Environment` | Host environment name; default `Development`.

`test.Configuration` exposes the resulting `IConfiguration`. Example: [`Configuration_Overrride_Use`](../tests/UnitTestEx.NUnit.Test/Other/GenericTest.cs), [`Configuration`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs), [`Configuration_Use`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

`appsettings.unittest.json` can also set the default JSON serializer for every test:

``` json
{
  "DefaultJsonSerializer": "UnitTestEx.MSTest.Test.NewtonsoftJsonSerializer, UnitTestEx.MSTest.Test"
}
```

The default is `UnitTestEx.Json.JsonSerializer` (System.Text.Json). Use `UseJsonSerializer(...)` to change it for one tester.

See also: [Test set-up and extensibility](./test-setup-and-extensibility.md).

[← Back to README](../README.md)
