# Assertions, expectations and logging

[← Back to README](../README.md)

<br/>

## Assert after, or Expect before

Each `Run` returns an **assertor** for chained `Assert*` calls. Add `using UnitTestEx.Expectations;` and you can instead declare `Expect*` calls **before** `Run`; they are checked automatically when the run completes. The two styles are equivalent. Expectations read well for "given/when/then" tests, and they also let a library add cross-cutting expectations through `TesterExtensionsConfig.ExpectationAssertAsync`.

``` csharp
// Assert after.
test.Controller<PersonController>().Run(c => c.Get(1)).AssertOK().AssertValue(expected);

// Expect before.
test.Controller<PersonController>()
    .ExpectStatusCode(HttpStatusCode.BadRequest)
    .ExpectErrors("First name is required.", "Last name is required.")
    .Run(c => c.Update(1, new Person()));
```

Example: [`Update_Test1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`Update_Test5_ExpectationFailure`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs), [`ExpectValue_Simple`](../tests/UnitTestEx.NUnit.Test/Other/ExpectationsTest.cs), [`ExpectValue_WithFunc`](../tests/UnitTestEx.NUnit.Test/Other/ExpectationsTest.cs).

<br/>

## Expectations

Expectation | Checks
-|-
`ExpectSuccess()` | No exception; for HTTP, a success status code.
`ExpectStatusCode(code)` | HTTP status code.
`ExpectException(message)` / `ExpectException().Any()` / `ExpectException().Type<T>(message?)` | An exception is thrown: with a given message, of any kind, or of a given type.
`ExpectError(message)` / `ExpectErrors(...)` | API errors (`ValidationProblemDetails` or a simple error dictionary), by message or by `ApiError(field, message)`.
`ExpectValue(value, pathsToIgnore)` / `ExpectValue(t => value)` | Result value; compared as JSON. The function form receives the tester, so the expected value can use its state.
`ExpectJson(json)` / `ExpectJsonFromResource(name)` | Result value against JSON text or an embedded resource.
`ExpectLogContains(text)` / `ExpectLogNotContains(text)` | Logs captured during the run.

Example: [`ExceptionSuccess_ExpectException_Type`](../tests/UnitTestEx.NUnit.Test/Other/ExpectationsTest.cs), [`ExpectError_None`](../tests/UnitTestEx.NUnit.Test/Other/ExpectationsTest.cs), [`Get_Test1`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs) (`ExpectLogContains`), [`Get_Test1_ExpectLogNotContains_Failure`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Assertors

Assertor | Returned by | Highlights
-|-|-
`VoidAssertor` | `Run(Action)`, void methods | `AssertSuccess`, `AssertException<T>(message)`
`ValueAssertor<T>` | `Run(Func<T>)`, `Type<T>().Run(...)` | `AssertValue`, `AssertJson`, `AssertJsonFromResource`, `AssertValueFromJsonResource`, `ToActionResultAssertor`, `ToHttpResponseMessageAssertor`
`HttpResponseMessageAssertor` (`<T>`) | API tests, Function HTTP triggers | `AssertOK`, `AssertCreated`, `AssertNotFound`, ... (one per common status), `AssertErrors`, `AssertContentType*`, `AssertLocationHeader`, `AssertETagHeader`, `AssertNamedHeader`, `AssertValue`, `AssertJson`
`ActionResultAssertor` | `IActionResult` results | Status and value assertions over `IActionResult`; `ToHttpResponseMessageAssertor` executes the result to get a real HTTP response.
`HttpResultAssertor` | `IResult` results | `ToHttpResponseMessageAssertor`

Every assertor exposes `LogMessages`, the logs captured during that run, so you can write custom assertions on them.

<br/>

## Comparing objects and JSON

All value and JSON comparisons serialize both sides to JSON and compare them with `JsonElementComparer`. This tolerates property ordering, and it reports **every** difference with its JSON path, not just the first. Exclude volatile members with `pathsToIgnore`, e.g. `"etag"`, `"changeLog"` or `"items.id"`.

Use `ObjectComparer.Assert(expected, actual, pathsToIgnore)` and `ObjectComparer.AssertJson(...)` for ad-hoc comparisons outside a tester. `UseJsonComparerOptions(...)` / `TestSetUp.JsonComparerOptions` control behaviour such as null and default handling.

`ValueAssertor<T>.AssertJson` and both `AssertJsonFromResource` overloads compare the expected JSON structure with the serialized actual value, using the tester's serializer and comparer options. Object property order and whitespace are insignificant, array order matters, and `pathsToIgnore` applies to object/array paths. A returned string is still a string value: use a JSON string literal as its expected JSON, even when that string happens to contain JSON text.

Example: [`MatchyMatchy`](../tests/UnitTestEx.NUnit.Test/Other/ObjectComparerTest.cs), [`PathsToIgnore_PropertyName`](../tests/UnitTestEx.NUnit.Test/Other/ObjectComparerTest.cs), [`PathsToIgnore_Json`](../tests/UnitTestEx.NUnit.Test/Other/ObjectComparerTest.cs), [`Get_Test5_AnonymousType_OK`](../tests/UnitTestEx.NUnit.Test/PersonControllerTest.cs).

<br/>

## Embedded resources

`Resource.GetString`, `GetJson`, `GetJsonValue<T>`, `GetStream`, `DeserializeJson<T>` and `DeserializeYaml<T>` read embedded resources from the test assembly. Use them for large expected payloads and request bodies, e.g. `CreateJsonHttpRequestFromResource`, `AssertJsonFromResource`, or `MockHttpClient` `WithJsonResource`.

<br/>

## Logging capture

Every tester routes the app's `ILogger` output to the test framework's output. NUnit/MSTest write to `TestContext`, and xUnit to `ITestOutputHelper`. This includes scopes and multi-line messages, and is filtered by `TestSetUp.MinimumLogLevel`. Logs are also captured per run for `LogMessages` and `ExpectLog*`.

- `Checkpoint(reason)` writes a marker and flushes logs captured so far. This is useful in multi-step tests.
- `Delay(duration, reason)` waits, then shows any logging that happened meanwhile, e.g. from background work.

Example: [`Checkpoint_WritesReasonAndFlushesLogsToOutput`](../tests/UnitTestEx.NUnit.Test/Other/CheckpointAndDelayTest.cs), [`Delay_DelaysAndSurfacesLoggingThatOccursDuringTheDelay`](../tests/UnitTestEx.NUnit.Test/Other/CheckpointAndDelayTest.cs), [`Test`](../tests/UnitTestEx.NUnit.Test/Other/LoggerTest.cs) (scopes and multi-line).

[← Back to README](../README.md)
