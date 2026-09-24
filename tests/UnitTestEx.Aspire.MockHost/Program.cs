// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx
//
// This is a template for UnitTestEx's recommended, self-hosted WireMock.Net pattern (see the README's "Aspire multi-host testing" section) - a plain console app that starts a
// WireMock.Net standalone server (WireMock.Net.StandAlone's WireMockServer.Start) bound to the port Aspire assigns via the 'PORT' environment variable (the same convention the
// official WireMock.Net.Aspire container resource uses), registering UnitTestEx.Aspire's JsonElementComparerMatcher as a custom matcher so this project resource's JSON body matching has
// genuine parity with UnitTestEx's own JsonElementComparer. See UnitTestEx.Aspire.MockHost.csproj's header comment for the full rationale. Consumers are expected to copy this
// Program.cs into their own solution (referencing the UnitTestEx.Aspire package, which ships JsonElementComparerMatcher and WireMockConsole) rather than reference this project as
// a package - UnitTestEx does not ship it as an executable.
//
// All of the port/settings/custom-matcher/graceful-shutdown boilerplate lives in UnitTestEx.Aspire's WireMockConsole.RunAsync - this Program.cs only needs to say how to start the
// actual WireMock.Net server itself (WireMockServer.Start, from the WireMock.Net package this project references).

using UnitTestEx.Aspire;
using WireMock.Server;

await WireMockConsole.RunAsync(settings => WireMockServer.Start(settings));
