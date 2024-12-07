<br/>

![Logo](./images/Logo256x256.png "UnitTestEx")

<br/>

## Introduction

_UnitTestEx_ provides [.NET testing](https://docs.microsoft.com/en-us/dotnet/core/testing/) extensions to the most popular testing frameworks: [MSTest](https://github.com/Microsoft/testfx-docs), [NUnit](https://nunit.org/) and [Xunit](https://xunit.net/).

The scenarios that _UnitTestEx_ looks to address is the end-to-end unit-style testing of the following whereby the capabilities look to adhere to the _AAA_ pattern of unit testing; Arrange, Act and Assert.

- [API Controller](#API-Controller)
- [HTTP-triggered Azure Function](#HTTP-triggered-Azure-Function)
- [Service Bus-trigger Azure Function](#Service-Bus-trigger-Azure-Function)
- [Generic Azure Function Type](#Generic-Azure-Function-Type)
- [HTTP Client mocking](#HTTP-Client-mocking)

<br/>

## Status

The build and packaging status is as follows.

CI | `UnitTestEx` | `UnitTestEx.MSTest` | `UnitTestEx.NUnit` | `UnitTestEx.Xunit`
-|-|-|-|-
[![CI](https://github.com/Avanade/UnitTestEx/workflows/CI/badge.svg)](https://github.com/Avanade/UnitTestEx/actions?query=workflow%3ACI) | [![NuGet version](https://badge.fury.io/nu/UnitTestEx.svg)](https://badge.fury.io/nu/UnitTestEx) | [![NuGet version](https://badge.fury.io/nu/UnitTestEx.MSTest.svg)](https://badge.fury.io/nu/UnitTestEx.MSTest) | [![NuGet version](https://badge.fury.io/nu/UnitTestEx.NUnit.svg)](https://badge.fury.io/nu/UnitTestEx.NUnit) | [![NuGet version](https://badge.fury.io/nu/UnitTestEx.Xunit.svg)](https://badge.fury.io/nu/UnitTestEx.Xunit)

The included [change log](CHANGELOG.md) details all key changes per published version.

<br/>

## API Controller

Leverages the [`WebApplicationFactory`](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests) (WAF) as a means to host a test server in process to invoke APIs directly using HTTP requests. This has the benefit of validating the HTTP pipeline and all Dependency Injection (DI) configuration within. External system interactions can be mocked accordingly.

_UnitTestEx_ encapsulates the `WebApplicationFactory` providing a simple means to arrange the input, execute (act), and assert the response. The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
using var test = ApiTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .Controller<ProductController>()
    .Run(c => c.Get("abc"))
    .AssertOK()
    .Assert(new { id = "Abc", description = "A blue carrot" });
```

<br/>

## HTTP-triggered Azure Function

Unfortunately, at time of writing, there is no `WebApplicationFactory` equivalent for Azure functions. _UnitTestEx_ looks to emulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the specified method. _UnitTestEx_ when invoking verifies usage of [`HttpTriggerAttribute`](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=csharp) and ensures a `Task<IActionResult>` result.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc", null), "abc", test.Logger))
    .AssertOK()
    .Assert(new { id = "Abc", description = "A blue carrot" });
```

Both the [_Isolated worker model_](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide) and [_In-process model_](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library) are supported.

Additionally, where an `HttpRequest` is used the passed `HttpRequest.PathAndQuery` is checked against that defined by the corresponding `HttpTriggerAttribute.Route` and will result in an error where different. The `HttpTrigger.WithRouteChecK` and `WithNoRouteCheck` methods control the path and query checking as needed.

<br/>

## Service Bus-trigger Azure Function

As above, there is currently no easy means to integration (in-process) test Azure functions that rely on the [Azure Service Bus](https://azure.microsoft.com/en-us/services/service-bus/). _UnitTestEx_ looks to emulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the specified method and verifies usage of the [`ServiceBusTriggerAttribute`](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-service-bus-trigger?tabs=csharp).

The following is an [example](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs) of invoking the function method directly passing in a `ServiceBusReceivedMessage` created using `test.CreateServiceBusMessageFromValue` (this creates a message as if coming from Azure Service Bus).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .ServiceBusTrigger<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
    .AssertSuccess();
```

Both the [_Isolated worker model_](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide) and [_In-process model_](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library) are supported.

<br/>

## Generic Azure Function Type

To support testing of any generic `Type` within an Azure Fuction, _UnitTestEx_ looks to simulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the specified method.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .Type<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
    .AssertSuccess();
```

<br/>

## Generic Type

To test a component that relies on Dependency Injection (DI) directly without the runtime expense of instantiating the underlying host (e.g. ASP.NET Core) the `GenericTester` enables any `Type` to be tested.

``` csharp
using var test = GenericTester.Create().ConfigureServices(services => services.AddSingleton<Gin>());
test.Run<Gin, int>(gin => gin.Pour())
    .AssertSuccess()
    .AssertValue(1);
```

<br/>

## DI Mocking

Each of the aforementioned test capabilities support Dependency Injection (DI) mocking. This is achieved by replacing the registered services with mocks, stubs, or fakes. The [`TesterBase`](./src/UnitTestEx/Abstractions/TesterBaseT.cs) enables using the `Mock*`, `Replace*` and `ConfigureServices` methods. 

The underlying `Services` property also provides access to the `IServiceCollection` within the underlying test host to enable further configuration as required.

<br/>

## HTTP Client mocking

Where invoking a down-stream system using an [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) within a unit test context this should generally be mocked. To enable _UnitTestEx_ provides a [`MockHttpClientFactory`](./src/UnitTestEx/Mocking/MockHttpClientFactory.cs) to manage each `HttpClient` (one or more), and mock a response based on the configured request. This leverages the [Moq](https://github.com/moq/moq4) framework internally to enable. One or more requests can also be configured per `HttpClient`.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX", new Uri("https://somesys"))
    .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

using var test = ApiTester.Create<Startup>();
test.ReplaceHttpClientFactory(mcf)
    .Controller<ProductController>()
    .Run(c => c.Get("abc"))
    .AssertOK()
    .Assert(new { id = "Abc", description = "A blue carrot" });
```

The `ReplaceHttpClientFactory` leverages the `Replace*` capabilities discussed earlier in [DI Mocking](#di-mocking).

<br/>

### HTTP Client configurations

Any configuration specified as part of the registering the `HttpClient` services from a Dependency Injection (DI) perspective is ignored by default when creating an `HttpClient` using the `MockHttpClientFactory`. This default behavior is intended to potentially minimize any side-effect behavior that may occur that is not intended for the unit testing. For example, a `DelegatingHandler` may be configured that requests a token from an identity provider which is not needed for the unit test, or may fail due to lack of access from the unit testing environment.

``` csharp
// Startup service (DI) configuration.
services.AddHttpClient("XXX", hc => hc.BaseAddress = new System.Uri("https://somesys")) // This is HttpClient configuration.
    .AddHttpMessageHandler(_ => new MessageProcessingHandler()) // This is HttpMessageHandler configuration.
    .ConfigureHttpClient(hc => hc.DefaultRequestVersion = new Version(1, 2)); // This is further HttpClient configuration.
```

However, where the configuration is required then the `MockHttpClient` can be configured _explicitly_ to include the configuration; the following methods enable:

Method | Description
-|-
`WithConfigurations` | Indicates that the `HttpMessageHandler` and `HttpClient` configurations are to be used. *
`WithoutConfigurations` | Indicates that the `HttpMessageHandler` and `HttpClient` configurations are _not_ to be used (this is the default state).
`WithHttpMessageHandlers` | Indicates that the `HttpMessageHandler` configurations are to be used. *
`WithoutHttpMessageHandlers` | Indicates that the `HttpMessageHandler` configurations are _not_ to be used.
`WithHttpClientConfigurations` | Indicates that the `HttpClient` configurations are to be used.
`WithoutHttpClientConfigurations` | Indicates that the `HttpClient` configurations are to be used.
-- | --
`WithoutMocking` | Indicates that the underlying `HttpClient` is **not** to be mocked; i.e. will result in an actual/real HTTP request to the specified endpoint. This is useful to achieve a level of testing where both mocked and real requests are required. Note that an `HttpClient` cannot support both, these would need to be tested separately.

_Note:_ `*` above denotes that an array of `DelegatingHandler` types to be excluded can be specified; with the remainder being included within the order specified.

``` csharp
// Mock with configurations.
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX").WithConfigurations()
    .Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

// No mocking, real request.
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX").WithoutMocking();
```

<br/>

### Times

To verify the number of times that a request/response is performed _UnitTestEx_ support MOQ [`Times`](https://github.com/moq/moq4/blob/main/src/Moq/Times.cs), as follows:

``` csharp
var mcf = MockHttpClientFactory.Create();
var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
mc.Request(HttpMethod.Post, "products/xyz").Times(Times.Exactly(2)).WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
    .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);
```

<br/>

### Sequeuce

To support different responses per execution MOQ supports [sequences](https://github.com/moq/moq4/blob/main/src/Moq/SequenceSetup.cs). This capability has been extended for _UnitTestEx_.

``` csharp
var mcf = MockHttpClientFactory.Create();
var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
mc.Request(HttpMethod.Get, "products/xyz").Respond.WithSequence(s =>
{
    s.Respond().With(HttpStatusCode.NotModified);
    s.Respond().With(HttpStatusCode.NotFound);
});
``` 

<br/>

### Delay

A delay (sleep) can be simulated so a response is not always immediated. This can be specified as a fixed value, or randomly generated using a from and to.

``` csharp
var mcf = MockHttpClientFactory.Create();
var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
mc.Request(HttpMethod.Get, "products/xyz").Respond.Delay(500).With(HttpStatusCode.NotFound);
mc.Request(HttpMethod.Get, "products/kjl").Respond.WithSequence(s =>
{
    s.Respond().Delay(250).With(HttpStatusCode.NotModified);
    s.Respond().Delay(100, 200).With(HttpStatusCode.NotFound);
});
```

<br/>

### YAML/JSON configuration

The Request/Response configuration can also be specified within an embedded resource using YAML/JSON as required. The [`mock.unittestex.json`](./src/UnitTestEx/Schema/mock.unittestex.json) JSON schema defines content; where the file is named `*.unittestex.yaml` or `*.unittestex.json` then the schema-based intellisense and validation will occur within the likes of Visual Studio.

To reference the YAML/JSON from a unit test the following is required:

``` csharp
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX", new Uri("https://unit-test")).WithRequestsFromResource("my.mock.unittestex.yaml");
```

The following represents a YAML example for one-to-one request/responses:

``` yaml
- method: post
  uri: products/xyz
  body: ^
  response:
    status: 202
    body: |
      {"product":"xyz","quantity":1}

- method: get
  uri: people/123
  response:
    body: |
      {
        "first":"Bob",
        "last":"Jane"
      }
```

The following represents a YAML example for a request/response with sequences:

``` yaml
- method: get
  uri: people/123
  sequence: 
    - body: |
        {
          "first":"Bob",
          "last":"Jane"
        }
    - body: |
        {
          "first":"Sarah",
          "last":"Johns"
        }
```

_Note:_ Not all scenarios are currently available using YAML/JSON configuration.

<br/>

## Expectations

By default _UnitTestEx_ provides out-of-the-box `Assert*` capabilities that are applied after execution to verify the test results. However, by adding the `UnitTestEx.Expectations` namespace in a test additional `Expect*` capabilities will be enabled (where applicable). These allow expectations to be defined prior to the execution which are automatically asserted on execution. 

The following is an [example](./tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

``` csharp
using var test = ApiTester.Create<Startup>();
test.Controller<PersonController>()
    .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
    .ExpectErrors(
        "First name is required.",
        "Last name is required.")
    .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }));
```

<br/>

## appsettings.unittest.json

_UnitTestEx_ supports the addition of a `appsettings.unittest.json` within the test project that will get loaded automatically when executing tests. This enables settings to be added or modified specifically for the unit testing external to the referenced projects being tested.

Additionally, this can also be used to change the default JSON Serializer for the tests. Defaults to `UnitTestEx.Json.JsonSerializer` (leverages `System.Text.Json`). By adding the following setting the default JSON serializer will be updated at first test execution and will essentially override for all tests. To change serializer for a specific test then use the test classes to specify explicitly.

``` json
{
  "DefaultJsonSerializer": "UnitTestEx.MSTest.Test.NewtonsoftJsonSerializer, UnitTestEx.MSTest.Test"
}
```

<br/>

## Examples

As _UnitTestEx_ is intended for testing, look at the tests for further details on how to leverage:
- [UnitTestEx.MSTest.Test](./tests/UnitTestEx.MSTest.Test)
- [UnitTestEx.NUnit.Test](./tests/UnitTestEx.NUnit.Test)
- [UnitTestEx.Xunit.Test](./tests/UnitTestEx.Xunit.Test)

_Note:_ There may be some slight variations in how the tests are constructed per test capability, this is to account for any differences between the frameworks themselves. For the most part the code should be near identical.

<br/>

## Other repos

These other _Avanade_ repositories leverage _UnitTestEx_ to provide unit testing capabilities:
- [_CoreEx_](https://github.com/Avanade/CoreEx) - Enriched capabilities for building business services by extending the core capabilities of .NET.
- [_Beef_](https://github.com/Avanade/Beef) - Business Entity Execution Framework to enable industralisation of API development.

<br/>

## License

_UnitTestEx_ is open source under the [MIT license](./LICENSE) and is free for commercial use.

<br/>

## Contributing

One of the easiest ways to contribute is to participate in discussions on GitHub issues. You can also contribute by submitting pull requests (PR) with code changes. Contributions are welcome. See information on [contributing](./CONTRIBUTING.md), as well as our [code of conduct](https://avanade.github.io/code-of-conduct/).

<br/>

## Security

See our [security disclosure](./SECURITY.md) policy.

<br/>

## Who is Avanade?

[Avanade](https://www.avanade.com) is the leading provider of innovative digital and cloud services, business solutions and design-led experiences on the Microsoft ecosystem, and the power behind the Accenture Microsoft Business Group.
