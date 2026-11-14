# Contributing to UnitTestEx

UnitTestEx is a multi-targeted .NET testing library. Preserve public API compatibility and established fluent patterns unless a change explicitly calls for a breaking release.

## Repository map

- `src/UnitTestEx`: API/Generic Host testers, framework integration, DI, mocking, assertions and logging.
- `src/UnitTestEx.Azure.Functions`: Azure Functions v4 isolated-worker invocation using the application's ordinary entry point.
- `src/UnitTestEx.Aspire`: real multi-process Aspire testing, readiness, resource logs and WireMock.
- `src/UnitTestEx.Azure.Functions.Durable`: optional preview SDK-host orchestration testing and a separate method-level virtual-time context.
- `tests`: framework suites, newer-major smoke tests, application/AppHost samples, Durable SDK-host tests and real-worker integration.
- `docs`: authoritative consumer guides. Start at the root README for the capability/proof-boundary matrix.
- `Common.targets`: shared versions, signing, targets-related defaults and package assets. Durable sets its preview `PackageVersion` after this import.

## Implementation conventions

Read the relevant code and tests before editing. Follow existing namespace/layout and fluent APIs; public factory and `With*` entry points live at their owning project roots. Put each top-level public class in its own file.

Document classes, properties and methods, including private members, with XML `<summary>`; document parameters/type parameters/returns on non-private APIs as appropriate. Do not needlessly split statements; lines around 220 characters are acceptable. Match surrounding formatting and keep explanatory comments limited to non-obvious logic.

Use normal consumer startup, not custom `Program`/`BuildHost` requirements or test-only application structure. Configure host replacements before initialization. Preserve disposal, scope ownership and per-test output routing; shared fixtures must not retain an individual test's output helper.

Framework integration belongs in core, not separate adapter packages. Preserve runtime support for xUnit v2/v3, NUnit v4/v5 and MSTest v3/v4. Prefer cached typed delegates over reflection in repeated assertion/output paths; avoid strong caches that unnecessarily retain collectible types.

Keep test proof boundaries honest: invocation is not Functions runtime integration; SDK-host replay is not worker/backend behavior; method-level virtual time is not an engine virtual clock. Durable remains preview and intentionally non-exhaustive.

Durable SDK-host self-discovery is opt-in and limited to selected assemblies; explicit root, activity and child registrations take precedence. Discovered instance activities own invocation scopes, while child orchestration scopes survive replay until run disposal. Real discovered activities can have side effects; preserve the explicit, discovery-with-override and discovered-child showcase variants.

## Build and focused validation

The full solution requires the .NET 11 SDK. Libraries target .NET 8/9/10/11; repository AppHosts target .NET 10 and Aspire/Durable integration test suites target .NET 10/11. Do not reduce published target frameworks to work around missing local prerequisites.

Examples from the repository root (adjust framework and filter for the change):

```shell
dotnet build UnitTestEx.sln -p:GeneratePackageOnBuild=false
dotnet test tests/UnitTestEx.NUnit.Test/UnitTestEx.NUnit.Test.csproj --framework net10.0 -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~GenericTest
dotnet test tests/UnitTestEx.Azure.Functions.Durable.Test/UnitTestEx.Azure.Functions.Durable.Test.csproj --framework net10.0 -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~FluentDurableTaskTest
dotnet test tests/UnitTestEx.Aspire.Xunit.Test/UnitTestEx.Aspire.Xunit.Test.csproj --framework net10.0 -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~AspireTesterTest.Http_Get_ReturnsSuccess
```

Use the smallest existing test selection covering the changed behavior; add focused regression tests for new/fixed behavior, not redundant assertions about language/framework behavior. Framework integration changes need the affected major-version smoke suites. See `.github/workflows/ci.yml` for CI commands and output checks.

Aspire tests launch real processes; some suites require containers. Real Durable worker integration additionally requires Azure Functions Core Tools and its backend/emulator. Report missing prerequisites explicitly. Run local integration target frameworks sequentially to avoid competing external resources; do not confuse local contention with a proven runtime regression.

## Documentation and package skills

Keep directly related guides and `CHANGELOG.md` current. Describe the final supported behavior, not the implementation journey.

Each published project owns a package `README.md` and one `skills/<name>/SKILL.md`. See [AI support](docs/ai-support.md). These are consumer assets, not contributor instructions. Keep standard skill frontmatter (`name` matching the directory and an activation-oriented `description`), concise task recipes and release-tagged links aligned with the shipped API and tests.

`Common.targets` packs the README and skills as inert files. Never add automatic consumer-repository instruction injection through NuGet targets. After packaging changes, pack the four libraries and inspect the actual `.nupkg` files for the expected README, nuspec readme metadata, versions, framework assets and skill paths.
