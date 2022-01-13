<br/>

![Logo](./images/Logo256x256.png "UnitTestEx")

<br/>

## Introduction

_UnitTestEx_ provides [.NET testing](https://docs.microsoft.com/en-us/dotnet/core/testing/) extensions to the most popular testing frameworks: [MSTest](https://github.com/Microsoft/testfx-docs), [NUnit](https://nunit.org/) and [Xunit](https://xunit.net/).

The scenarios that _UnitTestEx_ looks to address is the end-to-end unit-style testing of the following. The capabilities look to adhere to the AAA pattern of unit testing; Arrange, Act and Assert.

This framework looks to address the following testing scenarios:
- [API Controller](#API-Controller)
- [HTTP-triggered Azure Function](#HTTP-triggered-Azure-Function)
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
    .AssertOK(new { id = "Abc", description = "A blue carrot" });
```

<br/>

## HTTP-triggered Azure Function

Unfortunately, at time of writing, there is no `WebApplicationFactory` equivalent for Azure functions. _UnitTestEx_ looks to simulate by self-hosting the function, managing Dependency Injection (DI) configuration, and invocation of the underlying method.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
using var test = FunctionTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
    .HttpTrigger<ProductFunction>()
    .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person/abc", null), "abc", test.Logger))
    .AssertOK(new { id = "Abc", description = "A blue carrot" });
```

<br/>

## HTTP Client mocking

Where invoking a down-stream system using an [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) within a unit test context this should generally be mocked. To enable _UnitTestEx_ provides a [`MockHttpClientFactory`](./src/UnitTestEx/Mocking/MockHttpClientFactory.cs) to manage each `HttpClient`, and mock a response based on the configured request. This leverages the [Moq](https://github.com/moq/moq4) framework internally to enable.

The following is an [example](./tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

``` csharp
var mcf = MockHttpClientFactory.Create();
mcf.CreateClient("XXX", new Uri("https://somesys"))
    .Request(HttpMethod.Get, "products/abc").Respond.WithJson(new { id = "Abc", description = "A blue carrot" });

using var test = ApiTester.Create<Startup>();
test.ConfigureServices(sc => mcf.Replace(sc))
    .Controller<ProductController>()
    .Run(c => c.Get("abc"))
    .AssertOK(new { id = "Abc", description = "A blue carrot" });
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
