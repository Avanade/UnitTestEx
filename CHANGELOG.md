# Change log

Represents the **NuGet** versions.

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