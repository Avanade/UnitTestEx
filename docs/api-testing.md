# API testing

`ApiTester` (package `UnitTestEx`, plus your test framework package) wraps ASP.NET Core's [`WebApplicationFactory`](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) to host your API in-process. Requests go through the **full ASP.NET Core pipeline**: middleware, routing, model binding, filters, authorization and serialization. External dependencies are replaced through DI.

[← Back to README](../README.md)

<br/>

## Create

``` csharp
using var test = ApiTester.Create<Startup>(); // Or ApiTester.Create<Program>() for minimal hosting.
```

Where the API's `appsettings.json` isn't next to the test assembly, use `UseSolutionRelativeContentRoot("tests/My.Api")`. Example: [`Success2`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

<br/>

## Controller tests

`Controller<TController>().Run(c => c.Method(args))` takes the method call, works out the HTTP method, route, query string and body from the controller's attributes, and sends a **real HTTP request** through `TestServer`. This gives refactor-safe, strongly-typed tests that still exercise the whole pipeline.

``` csharp
test.Controller<PersonController>()
    .Run(c => c.Get(1))
    .AssertOK()
    .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
```

Example: [`Get_Test1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Get_Test4`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs) (not found), [`Update_Test1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`GetByArgs_Test1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Raw HTTP tests

`Http()` sends an explicit request. `Http<TResponse>()` also gives the response a type, so `ExpectValue` and `AssertValue` are strongly typed.

``` csharp
test.Http()
    .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" })
    .AssertOK();

test.Http<Person>()
    .ExpectStatusCode(HttpStatusCode.OK)
    .ExpectValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
    .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
```

Example: [`Http_Get1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Http_Update1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Http_Update_Http2`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Http_Update_Http3_AsJson`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Errors and problem details

`AssertErrors` understands both `ValidationProblemDetails` and the simple `{ field: [messages] }` error shape. `ApiError` also converts implicitly from a `(field, message)` tuple.

``` csharp
test.Http().Run(HttpMethod.Get, "product/test/problem").AssertErrors(new ApiError("id", "Not specified."));
test.Http<Person>().Run(HttpMethod.Post, "Person/88", p).Assert(HttpStatusCode.BadRequest, "No can do eighty-eight.");
```

Example: [`ValidationProblemDetails`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs), [`Http_Update_Http5`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Update_Test6`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## `IActionResult` without HTTP

To check an `IActionResult` created outside the pipeline, wrap it in a `ValueAssertor`. Then call `ToActionResultAssertor()`, or `ToHttpResponseMessageAssertor(httpRequest)` to run the result's `ExecuteResultAsync` and assert the HTTP response it produces. Example: [`Type_IActionResult`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Security

Method | Description
-|-
`UseUser(userName)` / `UseUser(object)` | Sets the test user name for all requests; it defaults to `TestSetUp.DefaultUserName` (`"Anonymous"`). UnitTestEx doesn't create claims or tokens itself. The user name is passed to `TestSetUp.OnBeforeHttpRequestMessageSendAsync`, where you add whatever your API's authentication expects, such as a bearer token or header.
`Http().WithUser(...)` / `Controller<T>().WithUser(...)` | Sets the user for a single tester.
`BypassAuthorization()` | Replaces `IAuthorizationHandler` with a handler that succeeds every requirement. Use it when a test isn't about authorization.

<br/>

## Cross-cutting run actions

`AddPreRunAction`, `AddPostRunBeforeExpectationsAction`, `AddPostRunAfterExpectationsAction` and `AddPostRunAction` register logic that runs for **every** `Run` on that tester. Use them for things like adding a standard expectation, without repeating it in each test.

<br/>

## Accessing the host

`test.Services` (the built `IServiceProvider`), `test.Configuration`, `test.GetTestServer()` and `test.GetLogger<T>()` give direct access for any arrangement or assertion the fluent API doesn't cover. Example: [`ServiceProvider`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs), [`Configuration`](../tests/UnitTestEx.NUnit.Test/ProductControllerTest.cs).

See also: [Dependency injection and configuration](./dependency-injection-and-configuration.md), [Assertions and expectations](./assertions-and-expectations.md), [HTTP client mocking](./http-client-mocking.md).

[← Back to README](../README.md)
