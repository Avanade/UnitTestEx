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
// A plain AddProject resource has no health check by default, so it is reported "Healthy" as soon as the OS
// process starts - not once Kestrel has actually bound its endpoint(s). WithHttpHealthCheck closes that race so
// that AspireTesterBase.WaitForResourceAsync (which waits on resource health) is a genuine readiness gate before
// tests attempt to call the resource.

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.UnitTestEx_Api>("api")
    .WithArgs("--framework", "net8.0")
    .WithHttpHealthCheck("/Person?firstName=health&lastName=check");

builder.Build().Run();
