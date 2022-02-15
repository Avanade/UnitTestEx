# Change log

Represents the **NuGet** versions.

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