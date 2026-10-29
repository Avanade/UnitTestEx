# HTTP client mocking

`MockHttpClientFactory` (package `UnitTestEx`) replaces `IHttpClientFactory` so that downstream HTTP calls made through named or typed `HttpClient`s return mocked responses. It is built on [Moq](https://github.com/moq/moq4). It works with every tester, and the same request/response definitions can be shared with Aspire tests through `IHttpMockClient`.

[← Back to README](../README.md)

<br/>

## Basics

Where invoking a down-stream system using an [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) within a unit test context this should generally be mocked. To enable _UnitTestEx_ provides a [`MockHttpClientFactory`](../src/UnitTestEx/Mocking/MockHttpClientFactory.cs) to manage each `HttpClient` (one or more), and mock a response based on the configured request. This leverages the [Moq](https://github.com/moq/moq4) framework internally to enable. One or more requests can also be configured per `HttpClient`.

The following is an [example](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

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

The `ReplaceHttpClientFactory` leverages the `Replace*` capabilities described in [Dependency injection and configuration](./dependency-injection-and-configuration.md).

Every mocked request/response pair is also logged (via `MockHttpClientHandler`) at `MockHttpClientFactory.LogLevel` - `LogLevel.Debug` by default; change it via `UseLogLevel(LogLevel)` (or set `LogLevel.None` to disable this logging entirely):

``` csharp
var mcf = MockHttpClientFactory.Create().UseLogLevel(LogLevel.Information);
```

<br/>

## HTTP Client configurations

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

## Request body matching

A request matches on method and URI, and then on body:

Method | Matches
-|-
_(none)_ / `WithNoBody()` | No body: absent or zero-length. In-process (Tier 1), this is the default when no body method is called; Aspire requires an explicit body method. On Aspire, `GET`/`HEAD` (or an unspecified method) apply no body constraint, as WireMock.Net does not read those bodies by default.
`WithAnyBody()` | Any body, including none.
`WithBody(text)` / `WithBody(text, mediaType)` | The exact text (and media type, where specified).
`WithJsonBody(json \| value, pathsToIgnore)` | Semantically equal JSON (via `JsonElementComparer`), ignoring the specified paths.
`WithJsonResourceBody(resourceName, ...)` | As `WithJsonBody`, with the JSON loaded from an embedded resource.

``` csharp
mc.Request(HttpMethod.Post, "products/xyz").WithAnyBody().Respond.With(HttpStatusCode.Accepted);
mc.Request(HttpMethod.Post, "products/abc").WithNoBody().Respond.With(HttpStatusCode.NoContent);
```

> In-process (Tier 1), where more than one stub matches a request, the **most recently** configured one wins (Moq semantics), so register catch-alls such as `WithAnyBody()` first and more specific stubs after.

<br/>

## Times

To verify the number of times that a request/response is performed _UnitTestEx_ support MOQ [`Times`](https://github.com/moq/moq4/blob/main/src/Moq/Times.cs), as follows:

``` csharp
var mcf = MockHttpClientFactory.Create();
var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
mc.Request(HttpMethod.Post, "products/xyz").Times(Times.Exactly(2)).WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
    .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);
```

<br/>

## Sequence

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

## Delay

A delay (sleep) can be simulated so a response is not always immediate. This can be specified as a fixed value, or randomly generated using a from and to.

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

## YAML/JSON configuration

The Request/Response configuration can also be specified within an embedded resource using YAML/JSON as required. The [`mock.unittestex.json`](../src/UnitTestEx/Schema/mock.unittestex.json) JSON schema defines content; where the file is named `*.unittestex.yaml` or `*.unittestex.json` then the schema-based intellisense and validation will occur within the likes of Visual Studio.

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
    headers:
      ETag: Abc123
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

The same YAML/JSON schema/file can also be loaded via the shared [`IHttpMockClient.WithRequestsFromResourceAsync`](../src/UnitTestEx/Mocking/IHttpMockClient.cs) - so a single embedded resource can configure both Tier 1's `MockHttpClient` above *and* Tier 2/3's `AspireHttpMockClient` (see [Aspire multi-host testing](./aspire.md) below) identically:

``` csharp
IHttpMockClient client = mcf.CreateClient("XXX", new Uri("https://unit-test")); // or tester.HttpMock("erp") for Aspire.
await client.WithRequestsFromResourceAsync<MyTestClass>("my.mock.unittestex.yaml");
```

Both loaders apply the same request `body` semantics: where `body` is omitted, the request must have **no body**, meaning absent or zero-length (`WithNoBody()`). Use `body: ^` to match any body (`WithAnyBody()`). An absent body and a zero-length body both send `Content-Length: 0` on the wire, so neither tier distinguishes them. One Aspire-tier caveat: by default WireMock.Net does not read `GET`/`HEAD` request bodies at all, so `WithNoBody()` applies no body constraint for those methods (or where the method is unspecified).

<br/>

<br/>

## Examples

Capability | Example
-|-
URI-only match | [`UriOnly_Single`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`UriOnly_Multi`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
URI and body (string/JSON/XML/any) | [`UriAndBody_Json_Single`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`UriAndBody_WithXmlRequest`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`UriAndAnyBody`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
No body (absent or zero-length) | [`Interface_Request_WithNoBody_MatchesAbsentOrEmptyBodyOnly`](../tests/UnitTestEx.Xunit.Test/HttpMockInterfaceTest.cs), [`HttpMock_WithNoBody_MatchesAbsentOrEmptyBodyOnly`](../tests/UnitTestEx.Aspire.Xunit.Test/AspireTesterTest.cs) (Aspire)
JSON response from resource | [`UriAndBody_WithJsonResponse3`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Verify call counts (Times) | [`VerifyMock_Executed`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`VerifyMock_NotExecuted_Times`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Sequence | [`MockSequence`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`MockSequence_Body`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Delay | [`MockDelay`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`MockSequenceDelay`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Reuse / reset / change on the fly | [`MockReuseSameAndReset`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`MockOnTheFlyChange`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Default (unnamed) HttpClient | [`DefaultHttpClient`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
YAML configuration | [`WithYamlRequests`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs), [`WithYamlSequence`](../tests/UnitTestEx.NUnit.Test/MockHttpClientTest.cs)
Used within an API test | [`Success`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs)

[← Back to README](../README.md)
