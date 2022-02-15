<br/>

![Logo](./images/Logo256x256.png "UnitTestEx")

<br/>

## Introduction

_UnitTestEx_ provides [.NET testing](https://docs.microsoft.com/en-us/dotnet/core/testing/) extensions to the most popular testing frameworks: [MSTest](https://github.com/Microsoft/testfx-docs), [NUnit](https://nunit.org/) and [Xunit](https://xunit.net/).

The scenarios that _UnitTestEx_ looks to address is the end-to-end unit-style testing of the following whereby the capabilities look to adhere to the AAA pattern of unit testing; Arrange, Act and Assert.

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

This leverages the [`WebApplicationFactory`](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests) (WAF) as a means to host a test server in process to invoke APIs directly using HTTP requests. This has the benefit of validating the HTTP pipeline and all Dependency Injection (DI) configuration within. External system interactions can be mocked accordingly.

_UnitTestEx_ encapsulates the `WebApplicationFactory` providing a simple means to arrange the input, execute (act), and assert the response. The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
using var test = ApiTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
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
test.ConfigureServices(sc => mcf.Replace(sc))
    .HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc", null), "abc", test.Logger))
    .AssertOK()
    .Assert(new { id = "Abc", description = "A blue carrot" });
```

<br/>

## Service Bus-trigger Azure Function

As above, there is currently no easy means to integration (in-process) test Azure functions that rely on the [Azure Service Bus](https://azure.microsoft.com/en-us/services/service-bus/). _UnitTestEx_ looks to emulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the specified method and verifies usage of the [`ServiceBusTriggerAttribute`](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-service-bus-trigger?tabs=csharp).

The following is an [example](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs) of invoking the function method directly passing in a `ServiceBusReceivedMessage` created using `test.CreateServiceBusMessage` (this creates a message as if coming from Service Bus).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
    .ServiceBusTrigger<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessage(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
    .AssertSuccess();
```

<br/>

### Service Bus Emulation

To enable in-process integration testing interacting with Azure Service Bus the [`ServiceBusTriggerTester.Emulate`](./src/UnitTestEx/Functions/ServiceBusTriggerTester.cs) method exposes the [`ServiceBusEmulatorTester`](./src/UnitTestEx/Functions/ServiceBusEmulatorTester.cs) type. This integrates directly with [Azure Service Bus](https://azure.microsoft.com/en-us/services/service-bus/) using the underlying functions configuration to determine connection string, etc. The `ServiceBus` _Queue_ or _Topic_ can be cleared (`Clear`), have test messages sent (`Send`), and then executed (`Run`).

The following is an [example](./tests/UnitTestEx.NUnit.Test/ServiceBusSimulationTest.cs).

``` csharp
using var test = FunctionTester.Create<Startup>(includeUserSecrets: true);
var r = test
    .ServiceBusTrigger<ServiceBusFunction>()
    .Simulate(nameof(ServiceBusFunction.Run2))
    .Clear()
    .SendValue(new Person { FirstName = "Bob", LastName = "Smith" })
    .Run()
    .AssertSuccess()
    .AssertMessageCompleted();
```

<br/>

## Generic Azure Function Type

To support testing of any generic `Type` within an Azure Fuction, _UnitTestEx_ looks to simulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the specified method.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ServiceBusFunctionTest.cs).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
    .Type<ServiceBusFunction>()
    .Run(f => f.Run2(test.CreateServiceBusMessage(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
    .AssertSuccess();
```

<br/>

## HTTP Client mocking

Where invoking a down-stream system using an [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) within a unit test context this should generally be mocked. To enable _UnitTestEx_ provides a [`MockHttpClientFactory`](./src/UnitTestEx/Mocking/MockHttpClientFactory.cs) to manage each `HttpClient` (one or more), and mock a response based on the configured request. This leverages the [Moq](https://github.com/moq/moq4) framework internally to enable. One or more requests can also be configured per `HttpClient`.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX", new Uri("https://somesys"))
    .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

using var test = ApiTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
    .Controller<ProductController>()
    .Run(c => c.Get("abc"))
    .AssertOK()
    .Assert(new { id = "Abc", description = "A blue carrot" });
```

</br>

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

<br>

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

<br>

## Examples

As _UnitTestEx_ is intended for testing, look at the tests for further details on how to leverage:
- [UnitTestEx.MSTest.Test](./tests/UnitTestEx.MSTest.Test)
- [UnitTestEx.NUnit.Test](./tests/UnitTestEx.NUnit.Test)
- [UnitTestEx.Xunit.Test](./tests/UnitTestEx.Xunit.Test)

_Note:_ There may be some slight variations in how the tests are constructed per test capability, this is to account for any differences between the frameworks themselves. For the most part the code should be near identical.

<br/>

## Other repos

These other _Avanade_ repositories leverage _UnitTestEx_ to provide unit testing capabilities:
- [Beef](https://github.com/Avanade/Beef) - Business Entity Execution Framework to enable industralisation of API development.

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
