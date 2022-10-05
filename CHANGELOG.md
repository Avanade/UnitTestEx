# Change log

Represents the **NuGet** versions.

## v1.0.26
- *Enhancement:* Moved `username` from all constructors to `UseUser(userName)` method. Additional, `WithUser(userName)` added to `ApiTester` to override the user name for a specific test invocation.
- *Enhancement:* All references to `Username` renamed to `UserName` for consistency with the .NET framework naming convention.
- *Enhancement:* Added `UsingApiTester` to provide a shared `ApiTester` instance per test class; versus instantiating per test method. Be aware that using may result in cross-test contamination.
- *Enhancement:* The `ILogger` test instances updated to use `TestSharedState` as a means to pass between hosted process and test process. Be aware that when tests are executed asynchronously there is currently no guarantee that the logs will be attributed to the correct test.
- *Enhancement:* Added `TestSetUp.Environment` to specify the .NET environment for the likes of configuration file loading; defaults to `Development`.
- *Enhancement:* The mocked `Response.RequestMessage` property is now updated with the initiating request.
- *Enhancement:* Support for mocked default (unnamed) `HttpClient` added via `MockHttpClientFactory.CreateDefaultClient` method.

## v1.0.25
- *Fixed:* Renamed `Expect` extensions methods to match convention consistently.
- *Enhancement:* Added `SystemExtensionMethods` class to enable the likes of `int.ToGuid()` for setting test-oriented `Guid` values.
- *Fixed:* `ApiTester` updated such that `appsettings.unittest.json` is optional.
- *Enhancement:* Added `ValidationTester` to support testing of an `IValidator` directly.
- *Enhancement:* Added `GenericTester` to support testing of any generic code directly.
- *Fixed:* Handle `AggregateException` by using its `InnerException` as the `Exception`.

## v1.0.24
- *Enhancement:* Updated the `ControllerTester` removing the HTTP request capabilies and moving into new `HttpTester`; this creates a more logical split as the latter needs no knowledge of the `Controller`. This new tester is available via `ApiTester.Http().Run(...)`.
- *Enhancement:* Added new `UnitTextEx.Expectations` namespace; when added will enable `Expect*` and `Ignore*` pre-execution assertions, that are then executed post `Run`/`RunAsync`. Some testers now also support the specification of a `TResponse` generic `Type` to enable further response value-related expectations.

## v1.0.23
- *Fixed:* The mock verification was not correctly updating the counter where there was a timeout. This has been corrected.

## v1.0.22
- *Enhancement:* Update `CoreEx` dependencies to `1.0.4`.
- *Fixed:* Updated the Mock verification code and customized implementation to improve checking and error message.

## v1.0.21
- *Enhancement:* Update `CoreEx` dependencies to `1.0.3`.
- *Enhancement:* Improve precision of milliseconds logging.

## v1.0.20
- *Fixed:* Expression invocation was incorrectly being invoked twice (and within the same scope), one with an await and the other without, racing each other and referencing the same DI scoped instances. 

## v1.0.19
- *Fixed:* Added extra validation to test methods that accept expressions to ensure only simple expressions (no method-chaining) are allowed. There are other non-expression methods that should be used to enable these more advanced scenarios. In doing so however, some validation and logging features may not work as well as the expression enabled functionalities.
- *Fixed:* Where using `MockHttpClientResponse.Delay` this was performing a `Thread.Sleep` internally which ignored the usage of the `CancellationToken` passed into the `SendAsync`. The `Thread.Sleep` has been replaced with a `Task.Delay` which is now passed the `CancellationToken` from the caller. This has also been extended where using `WithSequence`.

## v1.0.18
- *Fixed:* Prior change for `ApiTester` related to `appsettings.unittest.json` was not loading early enough and therefore not available for `startup` configuration activities. This has now been resolved by pre-reading and adding as in-process environment variables.
- *Enhancement:* Added `TypeTester` support to `ApiTester`.
- *Enhancement:* `TypeTester` updated to `Run` _synchronous_ methods as well as asynchronous.
- *[Issue 30](https://github.com/Avanade/UnitTestEx/issues/30)*: Added support to specify the default JSON serializer via `appsettings.unittest.json`. The following JSON will set the `CoreEx.Json.JsonSerializer.Default` to use `CoreEx.Newtonsoft.Json.JsonSerializer` versus the default `CoreEx.Text.Json.JsonSerializer`: `{ "DefaultJsonSerializer": "Newtonsoft.Json" }`.

## v1.0.17
- *Fixed:* `ApiTester` was not finding the `appsettings.json` from originating solution. Updated the content root explicitly with the current directory: `UseSolutionRelativeContentRoot(Environment.CurrentDirectory)`.
- *Enhancement:* Added support for finding `appsettings.unittest.json` file, always added last to override any other previous settings (including environment variables, etc.).
- *Fixed:* `MockHttpClientRequest` was incorrectly comparing the URL with an unencoded version; will always compare the original URL which must match on encodings also.

## v1.0.16
- *Enhancement:* Added additional `FunctionTesterBase.CreateHttpRequest` overloads to specify the `Content-Type`; defaults to `MediaTypeNames.Text.Plain`.
- *Enhancement:* **Breaking change.** Renamed `ControllerTester.RunWithRequest` to `RunHttpRequest` to be more aligned to `FunctionTesterBase`. Also updated to support same set of overloads for consistency.
- *Enhancement:* Improved the test logging such that the pre-execution details, such as HTTP request content, is output prior to execution to aid debugging of any exceptions/failures. 

## v1.0.15
- *Enhancement:* Added `ActionResultAssertor.GetValue` and `AssertJson` to be consistent with `HttpResponseMessageAssertor`.
- *Enhancement:* Added `AssertLocationHeader` and `AssertETagHeader` to both `HttpResponseMessageAssertor` and `ActionResultAssertor` (for `ValueContentResult`).
- *Enhancement:* Updated `JsonElementComparer` to return/list the differences found. Also, supports `PathsToIgnore` functionality.
- *Enhancement:* Added `MockHttpClientRequest.TraceRequestComparisons` that indicates whether the request content comparison differences should be trace logged to aid in debugging/troubleshooting.

## v1.0.14
- *Enhancement:* Added body value parameter to the `ControllerTester` to allow where not directly specified for the underlying run method.
- *Enhancement:* Added `HttpRequestOptions` parameter to the `ControllerTester` to allow further control and configuration of the `Uri` for the `Run` methods.
- *Enhancement:* Added some additional HTTP status code assertors.
- *Enhancement:* `HttpResponseMessageAssertor.AssertJson` added to support JSON comparisons where no `Type` is known.
- *Enhancement:* `HttpResponseMessageAssertor.GetValue<TCollResult, TColl, TItem>` added to support `ICollectionResult` responses.

## v1.0.13
- *[Issue 27](https://github.com/Avanade/UnitTestEx/issues/27)*: The `TypeTester` has been updated to ensure the result is logged correctly regardless of underlying `Type`.
- *[Issue 28](https://github.com/Avanade/UnitTestEx/issues/28)*: The `FunctionTester` has been updated to be more resilient to the JSON within `local.settings.json`; i.e. ignore comments and trailing commas.
- *Enhancement:* `ApiTester` and `FunctionTester` updated to provide a `GetLogger<TCategoryName>` method to simplify access to a typed logger.

## v1.0.12
- *Enhancement:* **Breaking change.** Integrate [`CoreEx`](https://github.com/Avanade/CoreEx/) package which primarily brings [`IJsonSerializer`](https://github.com/Avanade/CoreEx/blob/main/src/CoreEx/Json/IJsonSerializer.cs) functionality to enable configuration of either [`CoreEx.Text.Json.JsonSerializer`](https://github.com/Avanade/CoreEx/blob/main/src/CoreEx/Text/Json/JsonSerializer.cs) (default) or [`CoreEx.Newtonsoft.Json.JsonSerializer`](https://github.com/Avanade/CoreEx/blob/main/src/CoreEx.Newtonsoft/Json/JsonSerializer.cs). The `MockHttpClientFactory`, `ApiTester` and `FunctionTester` have new method `UseJsonSerializer` to individually update from the default. To change the default for all tests then set [`CoreEx.Json.JsonSerializer.Default`](https://github.com/Avanade/CoreEx/blob/main/src/CoreEx/Json/JsonSerializer.cs) to the desired serializer.
- *Enhancement:* Improved the replacement of the `MockHttpClientFactory` with the `ApiTester` and `FunctionTester`. Existing code `test.ConfigureServices(sc => mcf.Replace(sc))` can be replaced with `test.ReplaceHttpClientFactory(mcf)`.
- *Enhancement:* Added `ReplaceSingleton`, `ReplaceScoped` and `ReplaceTransient` methods directly to `ApiTester` and `FunctionTester`. For example, existing code `test.ConfigureServices(sc => sc.ReplaceTransient<XXX>())` can be replaced with `test.ReplaceTransient<XXX>()`.
- *Enhancement:* Added addtional `CreateHttpRequest` overloads to support additional parameters `HttpRequestOptions? requestOptions = null, params IHttpArg[] args` as enabled by `CoreEx`. These enable additional capabilities for the `HttpRequest` query string and headers.

## v1.0.11
- *[Issue 24](https://github.com/Avanade/UnitTestEx/issues/24)*: Added additional `IServiceCollection.Replace` extension methods to support `ReplaceXxx<T>()` and `ReplaceXxx<T, T>()` to match the standard `AddXxx` methods.

## v1.0.10
- *[Issue 22](https://github.com/Avanade/UnitTestEx/issues/22)*: `TypeTester.Run` and `RunAsync` methods updated to support `Func` versus `Expression<Func>` to simplify runtime usage.

## v1.0.9
- *[Issue 20](https://github.com/Avanade/UnitTestEx/issues/20)*: Enabled casting of a `ResultAssertor` to an `ActionResultAssertor` where the result `Type` is `IActionResult` via the `ResultAssertor.ToActionResultAssertor` method.
- *Enhancement:* Enabled casting of a `ResultAssertor` to an `HttpResponseMessageAssertor` where the result `Type` is `HttpResponseMessage` via the `ResultAssertor.HttpResponseMessageAssertor` method.

## v1.0.8
- *[Issue 18](https://github.com/Avanade/UnitTestEx/issues/18)*: `ActionResultAssertor.Assert` with object value was not performing correct comparison when result is `ContentResult` and the underlying `ContentType` was `Json`.
- *Enhancement:* Write the `Contents` to the test output where the result is `ContentResult`.

## v1.0.7
- *[Issue 12](https://github.com/Avanade/UnitTestEx/issues/12)*: `ObjectComparer.Assert` added for each test framework that compares two objects and will fail, and report, where there is not a match.
- *[Issue 14](https://github.com/Avanade/UnitTestEx/issues/14)*: Re-introduced [`ServiceBusTriggerTester`](./src/UnitTestEx/Functions/ServiceBusTriggerTester.cs) which manages execution and automatically logs the value associated with the trigger.
- *[Issue 14](https://github.com/Avanade/UnitTestEx/issues/14)*: The `ServiceBusTriggerTester.Emulate` ([`ServiceBusEmulatorTester`](./src/UnitTestEx/Functions/ServiceBusEmulatorTester.cs)) manages the execution of the `ServiceBusTriggerAttribue` function method by orchestrating Azure Service Bus integration in a similar manner as if the Azure function run-time proper had invoked.
- *[PR 16](https://github.com/Avanade/UnitTestEx/pull/16)*: Support all media types in `MockHttpClientRequest`.
- *Enhancement:* All `Run` methods now support a `RunAsync` where appropriate.

## v1.0.6
- *[Issue 10](https://github.com/Avanade/UnitTestEx/issues/10)*: **Breaking change.** Changed the `ActionResultAssertor.AssertAccepted` and `ActionResultAssertor.AssertCreated` to assert status only; the existing value check should be performed using the `ActionResultAssertor.Assert`. Pattern now is to check status and value separately (no longer all inclusive).
- *[Issue 10](https://github.com/Avanade/UnitTestEx/issues/10)*:  **Breaking change.** Changed the `HttpResponseMessageAssertor.AssertAccepted` and `HttpResponseMessageAssertor.AssertCreated` to assert status only; the existing value check should be performed using the `ActionResultAssertor.Assert`. Pattern now is to check status and value separately (no longer all inclusive).
- *[Issue 10](https://github.com/Avanade/UnitTestEx/issues/10)*:  **Breaking change.** Changed `ActionResultAssertor.AssertBadRequest` and `HttpResponseMessageAssertor.AssertBadRequest` to assert status only; added new `AssertErrors` to each for error message asserting.
- *[Issue 9](https://github.com/Avanade/UnitTestEx/issues/9)*:  Add `Services` property (`IServicesCollection`) to both `ApiTesterBase` and `FunctionTesterBase`. This allows direct access to the underlying `Services` outside of the more typical `Run`.

## v1.0.5
- *[Issue 7](https://github.com/Avanade/UnitTestEx/issues/7)*: Added delay (sleep) option so response is not always immediate.
- *Enhancement:* **Breaking change.** `Functions.GenericTriggerTester` replaced with `Hosting.TypeTester` as agnostic to any function trigger. `Functions.TriggerTesterBase` replaced with `Hosting.HostTesterBase` for same agnostic reasoning. `FunctionTestBase.GenericTrigger` method renamed to `FunctionTestBase.Type` so as to not imply a _trigger_ requirement (i.e. can be any _Type+Method_ that needs testing).

## v1.0.4
- *[Issue 3](https://github.com/Avanade/UnitTestEx/issues/3)*: Added support for MOQ `Times` struct to verify the number of times a request is made.
- *[Issue 4](https://github.com/Avanade/UnitTestEx/issues/4)*: Added support for MOQ sequences; i.e. multiple different responses.
- *[Issue 5](https://github.com/Avanade/UnitTestEx/issues/5)*: Deleted `MockServiceBus` as the mocking failed to work as intended. This has been replaced by `FunctionTesterBase` methods of `CreateServiceBusMessage`, `CreateServiceBusMessageFromResource` and `CreateServiceBusMessageFromJson`.

## v1.0.3
- *Fixed:* `MockHttpClientFactory.CreateClient` overloads were ambiquous, this has been corrected.
- *Fixed:* Resolved logging output challenges between the various test frameworks and `ApiTester` (specifically) to achieve consistent output.
- *Enhancement:* The logging output now includes scope details.
- *Added:* New `MockServiceBus.CreateReceivedMessage` which will mock the `ServiceBusReceivedMessage` and add the passed value as serialized JSON into the `Body` (`BinaryData`).

## v1.0.2
- *Added:* A new `GenericTestertrigger` has been enabled for non HTTP-triggered functions.
- *Added:* Assert capabilities, where applicable, support runtime `Exception` capturing, and have `AssertSuccess` and `AssertException` accordingly. There is a new `VoidAsserter` to ensure success or exception where a function is `void`.

## v1.0.1
- *New:* Initial publish to GitHub/NuGet.