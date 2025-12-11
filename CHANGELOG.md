# Change log

Represents the **NuGet** versions.

## v5.9.0
- *Enhancement:* Added `WithGenericTester` (_MSTest_ and _NUnit_ only) class to enable class-level generic tester usage versus one-off.
- *Enhancement:* Added `TesterBase.UseScopedTypeSetUp()` to enable a function that will be executed directly before each `ScopedTypeTester{TService}` is instantiated to allow standardized/common set up to occur.

## v5.8.0
- *Enhancement:* Extended the `MockHttpClientResponse.With*` methods to support optional _media type_ parameter to enable specification of the `Content-Type` header value.
- *Enhancement:* Added `HttpResponseMessageAssertor.AssertContentTypeProblemJson` to enable asserting that the content type is `application/problem+json`.
- *Fixed:* `HttpResponseMessageAssertor<TValue>.Value` no longer asserts the content type as `application/json` by default as this could not be overridden; this should be asserted explicitly using `AssertContentType()`, `AssertContentTypeJson()`, `AssertContentTypeProblemJson`, etc.

## v5.7.0
- *Enhancement:* Added `.NET10.0` support to all `UnitTestEx` packages.
- *Enhancement:* Added `AssertNoNamedHeader` to the `HttpResponseMessageAssertor` to enable asserting that a named header is not present in the response.

## v5.6.3
- *Fixed:* `TestSetUp` fixed to load _all_ assemblies on start up (versus selective) to ensure all `OneOffTestSetUpAttribute` implementations are executed prior to any test executions.
- *Fixed:* Added `IJsonSerializer.Clone()` and `JsonElementComparerOptions.Clone` to ensure cross test contamination does not occur.

## v5.6.2
- *Fixed:* Republish packages with a new version; last publish was incomplete.

## v5.6.1
- *Fixed:* The `ValueExpectations` corrected to ensure that the expected value is correctly compared against the actual value.

## v5.6.0
- *Enhancement:* The `RunAsync` methods updated to support `ValueTask` as well as `Task` for the `TypeTester` and `GenericTester` (.NET 9+ only).
- *Enhancement:* Added `HttpResultAssertor` for ASP.NET Minimal APIs `Results` (e.g. `Results.Ok()`, `Results.NotFound()`, etc.) to enable assertions via the `ToHttpResponseMessageAssertor`.
- *Enhancement:* `TesterBase<TSelf>` updated to support keyed services.
- *Enhancement* `ScopedTypeTester` created to support pre-instantiated scoped service where multiple tests can be run against the same scoped instance. The existing `TypeTester` will continue to directly execute a one-off scoped instance. These now exist on the `TesterBase<TSelf>` enabling broader usage.
- *Enhancement:* Added `TesterBase<TSelf>.Delay` method to enable delays to be added in a test where needed.
- *Fixed:* The `ExpectationsArranger` updated to `Clear` versus `Reset` after an assertion run to ensure no cross-test contamination.

## v5.5.0
- *Enhancement:* The `GenericTester` where using `.NET8.0` and above will leverage the new `IHostApplicationBuilder` versus existing `IHostBuilder` (see Microsoft [documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) and [recommendation](https://github.com/dotnet/runtime/discussions/81090#discussioncomment-4784551)). Additionally, if a `TEntryPoint` is specified with a method signature of `public void ConfigureApplication(IHostApplicationBuilder builder)` then this will be automatically invoked during host instantiation. This is a non-breaking change as largely internal.

## v5.4.6
- *Fixed:* Added `TestFrameworkImplementor.SetLocalCreateFactory` to Xunit `ApiTestFixture` constructor to ensure set correctly for the `OnConfiguration` method override.

## v5.4.5
- *Fixed:* Added `TesterBase.JsonMediaTypeNames` which provides a list of valid JSON media types to be used to determine JSON-related payloads in tests.
- *Fixed:* Added Xunit `ApiTestFixture.OnConfiguration` to enable configuration to be perform prior to test execution.

## v5.4.4
- *Fixed:* The `XunitLocalTestImplementor.SetLocalImplementor` has been made public.
- *Fixed:* Added `TesterBase.ReplaceTestFrameworkImplementor` to enable dynamic replacement.

## v5.4.3
- *Fixed:* The `ValueAssertor.Result` is being obsoleted and replaced with `ValueAssertor.Value` to be more explicit. The `Result` property will be removed in a future version.
- *Fixed:* The `ValueAssertor` JSON-based assertions updated to serialize the `Value` and compare; versus, serializing the JSON and then comparing.
- *Fixed:* The `ObjectComparer.JsonAssert` is being obsoleted and replaced with `ObjectComparer.AssertJson` to be more consistent. The `JsonAssert` method will be removed in a future version.

## v5.4.2
- *Fixed:* The `HttpResponseMessageAssertorBase.AssertErrors` has been extended to check for both `IDictionary<string, string[]>` (previous) and `HttpValidationProblemDetails` (new) HTTP response JSON content.
- *Fixed:* The `HttpResponseMessageAssertorBase.AssertJson` corrected to not validate the content type as this should be handled by the `AssertContentType` method. The `AssertJson` method now only checks the content against the expected JSON value.

## v5.4.1
- *Fixed:* The `ToHttpResponseMessageAssertor` supports a new `HttpRequest` parameter to enable access to the originating `HttpContext` such that its `HttpResponse` is used; versus, creating new internally.

## v5.4.0
- *Enhancement:* All `CreateHttpRequest` and related methods moved to `TesterBase` to ensure availability for all derived testers.
- *Enhancement:* Added `ToHttpResponseMessageAssertor` to `ActionResultAssertor` and `ValueAssertor` that converts an `IActionResult` to an `HttpResponseMessage` and returns an `HttpResponseMessageAssertor` for further assertions. The underlying `Host` must be configured (DI) correctly for this to function; otherwise, an exception will be thrown.
- *Enhancement:* Added `WithApiTester` base class per test framework to provide a shared `ApiTester` instance per test class; versus instantiating per test method. Be aware that using may result in cross-test contamination.

## v5.3.0
- *Enhancement:* Added `MockHttpClientResponse.Header` methods to enable the specification of headers to be included in the mocked response.
  - The `MockHttpClient.WithRequestsFromResource` YAML/JSON updated to also support the specification of response headers.   

## v5.2.0
- *Enhancement:* Added `TesterBase<TSelf>.UseAdditionalConfiguration` method to enable additional configuration to be specified that overrides the `IHostBuilder` as the host is being built. This leverages the underlying `IConfigurationBuilder.AddInMemoryCollection` capability to add. This is intended to support additional configuration that is not part of the standard `appsettings.json` or `appsettings.unittest.json` configuration.

## v5.1.0
- *Enhancement:* Where an `HttpRequest` is used for an Azure Functions `HttpTriggerTester` the passed `HttpRequest.PathAndQuery` is checked against that defined by the corresponding `HttpTriggerAttribute.Route` and will result in an error where different. The `HttpTrigger.WithRouteChecK` and `WithNoRouteCheck` methods control the path and query checking as needed.

## v5.0.0
- *Enhancement:* `UnitTestEx` package updated to include only standard .NET core capabilities; new packages created to house specific as follows:
  - `UnitTestEx.Azure.Functions` created to house Azure Functions specific capabilities;
  - `UnitTestEx.Azure.ServiceBus` created to house Azure Service Bus specific capabilities;
  - This allows for more focused testing capabilities and provides a common pattern for ongoing extensibility; whilst also looking to limit cross package dependency challenges.
  - Existing usage will require references to the new packages as required. There should be limited need to update existing tests to use beyond the requirement for the root `UnitTestEx` namespace. The updated default within `UnitTestEx` is to expose the key capabilities from the root namespace. For example, `using UnitTestEx.NUnit`, should be replaced with `using UnitTestEx`.
- *Enhancement:* Updated `UnitTestEx.Xunit` to align with `UnitTestEx.NUnit` and `UnitTestEx.MSTest` for consistency; the following `UnitTestBase` methods have been removed and should be replaced with:
  - `CreateMockHttpClientFactory()` replaced with `MockHttpClientFactory.Create()`;
  - `CreateGenericTester()` replaced with `GenericTester.Create()`;
  - `CreateApiTester<TStartup>()` replaced with `ApiTester.Create<TStartup>()`;
  - `CreateFunctionTester<TStartup>()` replaced with `FunctionTester.Create<TStartup>()`.

## v4.4.2
- *Fixed*: Updated `System.Text.Json` package depenedency to latest; resolve [Microsoft Security Advisory CVE-2024-43485](https://github.com/advisories/GHSA-8g4q-xg66-9fp4).

## v4.4.1
- *Fixed:* Updated all package depenedencies to latest.

## v4.4.0
- *Enhancement:* Added `ExpectJson` and `ExpectJsonFromResource` to `IValueExpectations` to enable value comparison against the specified (expected) JSON.

## v4.3.2
- *Fixed*: Added `TraceRequestComparisons` support to `MockHttpClient` to enable tracing for all requests.

## v4.3.1
- *Fixed*: Added `StringSyntaxAttribute` support to improve intellisense for JSON and URI specification.

## v4.3.0
- *Enhancement:* A new `MockHttpClient.WithRequestsFromResource` method enables the specification of the Request/Response configuration from a YAML/JSON embedded resource. The [`mock.unittestex.json`](./src/UnitTestEx/Schema/mock.unittestex.json) JSON schema defines content.

## v4.2.0
- *Enhancement:* Any configuration specified as part of registering the `HttpClient` services from a Dependency Injection (DI) perspective is ignored by default when creating an `HttpClient` using the `MockHttpClientFactory`. This default behavior is intended to potentially minimize any side-effect behavior that may occur that is not intended for the unit testing. See [`README`](./README.md#http-client-configurations) for more details on capabilities introduced; highlights are:
  - New `MockHttpClient.WithConfigurations` method indicates that the `HttpMessageHandler` and `HttpClient` configurations are to be used.
  - New `MockHttpClient.WithoutMocking` method indicates that the underlying `HttpClient` is **not** to be mocked; i.e. will result in an actual/real HTTP request to the specified endpoint. This will allow the mixing of real and mocked HTTP requests within the same test.

## v4.1.2
- *Fixed*: The `AssertLocationHeader` has been corrected to also support the specification of the `Uri` as a string. Additionally, contains support has been added with `AssertLocationHeaderContains`.

## v4.1.1
- *Fixed:* The `TypeTester` was not correctly capturing and outputting any of the logging, and also (as a result) the `ExpectLogContains` was not functioning as expected.

## v4.1.0
- *Enhancement:* Removed the `FunctionsStartup` constraint for `TEntryPoint` to enable more generic usage.
- *Enhancement:* Enable `Microsoft.Azure.Functions.Worker.HttpTriggerAttribute` (new [_isolated_](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide) function support), in addition to existing `Microsoft.Azure.WebJobs.HttpTriggerAttribute` (existing [_in-process_](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library) function support), within `HttpTriggerTester`.
- *Enhancement:* Enable `Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute` (new [_isolated_](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide) function support), in addition to existing `Microsoft.Azure.WebJobs.ServiceBusTriggerAttribute` (existing [_in-process_](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library) function support), within `ServiceBusTriggerTester`.
  - Additionally, `CreateServiceBusMessageActions` is being renamed to `CreateWebJobsServiceBusMessageActions`; a new `CreateWorkerServiceBusMessageActions` has been introduced to support _isolated_ `Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute` testing.
- *Enhancement*: Upgraded `NUnit` dependency to `4.0.1`; all unit tests now leverage the `NUnit` constraint model testing approach.
  - **Note**: Also, as a result it is recommended prior to upgrading to `v4.1.0`, where using `NUnit`, that all existing unit tests are updated to use the new constraint model testing approach; see [migration guide](https://docs.nunit.org/articles/nunit/release-notes/Nunit4.0-MigrationGuide.html) for details.

## v4.0.1
- *Fixed:* The `FunctionTesterBase` was updated to correctly load the configuration in the order similar to that performed by the Azure Functions runtime fabric.
- *Fixed:* Removed all dependencies to `Newtonsoft.Json`; a developer will need to explicitly add this dependency and `IJsonSerializer` implementation where applicable.

## v4.0.0
All internal dependecies to `CoreEx` have been removed. This is intended to further generalize the capabilities of `UnitTestEx`; but more importantly, break the circular dependency reference between the two repositories. New `CoreEx.UnitTesting*` packages have been created to extend the `UnitTestEx` capabilities for `CoreEx`.
 - *Enhancement:* All typed value assertions have been named `AssertValue` for consistency; otherwise, `AssertContent` for a simple string comparison.
 - *Enhancement:* All JSON-related assertions have been named `AssertJson*` for consistency.
 - *Enhancement:* The `CreateServiceBusMessage` methods that accept a generic `T` value have been renamed to `CreateServiceBusMessageFromValue`.
 - *Enhancement:* The `Expectations` capabilities have been greatly improved to support new expectations to be added/extended.
 - *Enhancement:* A new `GenericTester` that explicitly supports a `TValue` has been added; use new `GenericTester.CreateFor<TValue>` to instantiate/use.
 - *Enhancement:* Removed `KellermanSoftware.CompareNetObjects` dependency; all comparisons use internal `JsonElementComparer` which has proper/improved support for fully qualified paths, including optional array indexers. The related `JsonElementComparerOptions` provides a means to control the comparison behaviour. 
 - *Enhancement:* The `ObjectComparer` has been reinstated and now leverages the `JsonElementComparer` internally.
 - *Enhancement:* Updated to only support `.NET6.0` and above; added `.NET8.0` support.

The enhancements have been made in a manner to maximize backwards compatibility with previous versions of `UnitTestEx` where possible; however, some breaking changes were unfortunately unavoidable (and made to improve overall). There may be an opportunity for the consuming developer to add extension methods to support the previous naming conventions if desired; note that the next `CoreEx` version (`v3.6.0`) will implement extensions in new `CoreEx.UnitTesting` packages to support existing behaviors (where applicable).

## v3.1.0
- *Enhancement:* Updated all package depenedencies to latest.
- *Enhancement:* The `GenericTester` updated to support `IHostStartup` to enable shared host dependency injection configuration.
- *Enhancement:* Added `IEventExpectations<TSelf>` and `ILoggerExpectations<TSelf>` support to `GenericTester` and `ValidationTester`.
- *Enhancement:* Moved the `CreateServiceBusMessage` and related methods from `FunctionTesterBase` up the inheritance hierarchy to `TesterBase<TSelf>` to enable broader usage.
- *Enhancement:* Added `ExpectEventFromJsonResource` and `ExpectDestinationEventFromJsonResource` expectations to simplify specification versus having to instantiate `EventData`.
- *Enhancement:* The `JsonElementComparer` now defaults to case-insensitive comparison.
- *Enhancement:* All internal usage of the `ObjectComparer` replaced with usage of the `JsonElementComparer` to break external dependency. All `MembersToIgnore` have been replaced with `PathsToIgnore` (being the fully-qualified JSON path) as this is more explicit and less error prone. The `ObjectComparer` has been flagged as `Obsolete` and will be removed in a later version.

## v3.0.0
- *Enhancement:* Updated `CoreEx` dependencies to `3.0.0` as breaking changes were introduced. There are no breaking changes within `UnitTestEx` as a result; primarily related to the key `CoreEx` dependency.
  - Given the `CoreEx` dependency, explicitly the creation of the new `CoreEx.AspNetCore`, this `UnitTextEx` version is not backwards compatible with previous versions of `CoreEx` (i.e. `2.x.x`).
- *Enhancement:* Updated all package dependencies to latest.
- *Fixed:* Corrected the log output to ensure they appear in sequence logged.

## v2.2.3
- *Fixed:* The `ServiceBusMessageActionsAssertor` logging now logs regardless of whether it is the last parameter in the method being executed.
- *Fixed:* The loading of the `appsettings.unittest.json` has been moved after the `FunctionsStartup.ConfigureAppConfiguration` to override correctly.

## v2.2.2
- *Fixed:* Extended the `FunctionTesterBase` to enable `CreateServiceBusMessageActions` and `CreateServiceBusSessionMessageActions` similar to	`CreateServiceBusMessage` to enable mocked, unit testable, assert enabled, actions.

## v2.2.1
- *Fixed:* The `MockHttpClientRequest` request Uri validation fixed.

## v2.2.0
- *Fixed:* The `MockHttpClientRequest` request validation predicate has been improved to handle URL encoding.
- *Enhancement:* The `FunctionTesterBase` has been extended to support the creation of a `ServiceBusReceivedMessage` using  `CreateServiceBusMessage(EventData)`, `CreateServiceBusMessage(ServiceBusMessage)` and `CreateServiceBusMessage(AmqpAnnotatedMessage)`.
- *Enhancement:* Added `ExpectLogContains` expectation to confirm that the log output contains the specified text.
- *Enhancement:* Updated all package dependencies to latest.
- *Enhancement:* Added `.NET7.0` support to all `UnitTestEx` packages.

## v2.1.3
- *Fixed:* The `EventExpectations` internally assigned [members to ignore](https://github.com/GregFinzer/Compare-Net-Objects/wiki/Ignoring-Members) updated to use `ClassName.MemberName` syntax to explicitly ignore.
- *Fixed:* Added `ExpectEventValue` and `ExpectDestinationEventValue` expectations to simplify specification versus having to instantiate `EventData` with expected `Value`.
- *Fixed:* To remove any `EventData.Value` implementation (`Type`) differences the `EventData` is now serialized during runtime publish, then deserialized prior to expectation check within test.

## v2.1.2
- *Issue [52](https://github.com/Avanade/UnitTestEx/issues/52):* `UnitTestBase.TestServer` is now `public` (versus previous `protected`).
- *Issue [51](https://github.com/Avanade/UnitTestEx/issues/51):* Anonymous types create read-only properties; these were by default ignored when comparing. Read-only properties are now included by default within the `ObjectComparer`; note that these defaults can be overridden where applicable.

## v2.1.1
- *Fixed:* Incorrect package deployment; corrected.

## v2.1.0
- *Enhancement:* Added `TestSetUp.RegisterAutoSetUp` that will automatically throw a `TestSetUpException` where unsuccessful; otherwise, queues the successful output message. This is required as most testing frameworks do not allow for a log write during construction or fuxture set up. The underlying _UnitTestEx_ test classes will automatically log write where entries are discovered in the queue. This will at least ensure one of the underlying tests will output the success output where applicable. Otherwise, to log write explicitly use `TestSetUp.LogAutoSetUpOutputs`.

## v2.0.0
- *Enhancement:* Updated `CoreEx` dependencies to `2.0.0` as breaking changes were introduced. There are no breaking changes within `UnitTestEx` as a result; primarily related to the key `CoreEx` dependency.

## v1.0.27
- *Enhancement:* `TestSetUp` cloned (from `TestSetUp.Default`) per `TesterBase` instance to allow specific test changes.
- *Enhancement:* `EventExpectations` now supports `HasEvents` which simply verifies that one or more events were sent (ignores contents). 
- *Enhancement:* `ValidationTester` extended to support `RunCode` methods that execute passed action/function then catch and validate any thrown `ValidationException`.
- *Enhancement:* Added user identifier (`object`) support for `UseUser` and `WithUser` that leverages the `TestSetUp.UserNameConverter`.
- *Enhancement:* Underlying host created within the context of a `lock` to ensure thread-safety. Protections now in place to prohibit further changes once host has been created (`ResetHost` added to explicitly enable further changes if/when needed).
- *Enhancement* `HttpTesterBase` supports multi-threaded logging leveraging the new `unit-test-ex-request-id` header to coordinate between test and api.

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