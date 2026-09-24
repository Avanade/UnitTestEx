// This AppHost exists solely to provide a real, minimal .NET Aspire distributed application for exercising
// UnitTestEx.Aspire's AspireTesterBase<TAppHost, TSelf> capability in the UnitTestEx.Aspire.*.Test projects.
//
// It references the real, multi-targeted UnitTestEx.Api sample (also used by the Tier 1 suites) rather than a
// bespoke single-TFM sample, so the same representative app is exercised end-to-end for both tiers. This
// AppHost is deliberately pinned to a single, fixed TFM (Aspire's AppHost SDK does not support a multi-targeted
// AppHost project) - but that's fine: Aspire's whole premise is that the orchestrator/host doesn't need to
// match the TFM of the resources (or callers) it manages. The UnitTestEx.Aspire.*.Test projects that actually
// exercise UnitTestEx.Aspire's multi-targeted library code (net8.0/net9.0/net10.0) can all reference this one
// fixed AppHost, since a higher-TFM project can reference a lower-TFM one.
//
// Aspire's DCP orchestrator cannot disambiguate a multi-targeted project resource on its own (see
// https://github.com/dotnet/aspire/issues/2962), so the framework is passed explicitly via '--framework'.
//
// Endpoints are declared explicitly (launchProfileName: null + WithHttpsEndpoint) rather than relying on
// AddProject's implicit launchSettings.json-derived endpoint detection: that detection proved unreliable for a
// multi-targeted referenced project (UnitTestEx.Api targets net8.0/9.0/10.0) - it worked locally but failed on
// Linux CI with "no endpoint was found matching one of the specified names: https, http". A single explicit
// https endpoint avoids that ambiguity entirely (UnitTestEx.Api unconditionally redirects http to https anyway).
//
// A plain AddProject resource has no health check by default, so it is reported "Healthy" as soon as the OS
// process starts - not once Kestrel has actually bound its endpoint(s). WithHttpHealthCheck closes that race so
// that AspireTesterBase.WaitForResourceAsync (which waits on resource health) is a genuine readiness gate before
// tests attempt to call the resource.
//
// The "mockhost" resource is UnitTestEx's recommended, self-hosted WireMock.Net pattern: an ordinary project resource
// (UnitTestEx.Aspire.MockHost - a *sample* a consumer would copy into their own solution, not a package UnitTestEx
// ships) that self-hosts the WireMock.Net engine directly (WireMock.Net.StandAlone), rather than the official
// WireMock.Net.Aspire package's container resource. It stubs the "api" resource's "XXX" external HTTP dependency
// (see UnitTestEx.Api's Startup.cs and ProductController) for the Tier 2 HttpMock tests. Because it's just our own
// process, it registers a custom IMatcher (UnitTestEx.Aspire's JsonElementComparerMatcher) that delegates JSON body matching to UnitTestEx's
// own JsonElementComparer - see AspireTesterBase.HttpMock and AspireHttpMockRequest.WithJsonBodyUsingUnitTestExComparer
// - giving genuine JSON comparison-semantics parity with Tier 1 (including semantic date/GUID/number coercion; use
// JsonElementComparerOptions.Exact instead for WireMock-style strict textual matching) rather than being limited to
// WireMock.Net's own JSON comparison (see AspireHttpMockRequest.WithJsonBody's remarks). It also needs no
// Docker/Podman at all, unlike the official container resource - which remains a fully supported alternative for
// teams already standardized on it, simply by adding it directly to their own AppHost (builder.AddWireMock(name)
// from the WireMock.Net.Aspire package); UnitTestEx's HttpMock works against either resource type identically.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// dotnet dev-certs https --trust is not fully supported on Linux, so the ASP.NET Core dev cert used by the
// "api" resource's https endpoint is not OS-trusted on Linux CI runners. Both the health check probe above and
// AspireTesterBase's CreateHttpClient() resolve their HttpClient via this same DI container's IHttpClientFactory,
// so disabling certificate validation here (dev/test-only AppHost, never shipped) covers both.
builder.Services.ConfigureHttpClientDefaults(http =>
    http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }));

// IsRunMode: never surface this test-only resource in a published manifest (per the self-hosted WireMock.Net housekeeping note - see AppHost.cs's header comment).
var mockhost = builder.AddMockHostProject<Projects.UnitTestEx_Aspire_MockHost>("mockhost");

builder.AddProject<Projects.UnitTestEx_Api>("api", launchProfileName: null)
    .WithHttpsEndpoint(name: "https")
    .WithArgs("--framework", "net8.0")
    .WithHttpHealthCheck("/Person?firstName=health&lastName=check", endpointName: "https")
    // "mockhost" only exists in run mode (see the note above); WithMockHostEnvironment no-ops when it's null
    // rather than requiring an inline "if" guard at this (or any future) optional-resource call site.
    .WithMockHostEnvironment("XXX__BaseUrl", mockhost, "http");

builder.Build().Run();

