# UnitTestEx

API and Generic Host testing for .NET 8/9/10/11 with dependency overrides, HttpClient mocking, fluent assertions and captured logging.

## Quick start

Install `UnitTestEx` **7.0.0** alongside your NUnit, MSTest or xUnit framework and runner packages.

```csharp
using UnitTestEx;

using var test = GenericTester.Create();
test.Run(() => 1).AssertSuccess().AssertValue(1);
```

`ApiTester.Create<TEntryPoint>()` hosts ASP.NET Core through `WebApplicationFactory`; controller expressions still exercise the HTTP pipeline. Use `GenericTester` for DI-composed code without an API.

Framework detection supports xUnit v2/v3, NUnit v4/v5 and MSTest v3/v4. Separate UnitTestEx adapter packages are not required. For xUnit pass the per-test helper as `output`, or supply it to `UnitTestEx.Abstractions.UnitTestBase`. Explicit implementor factories own output handling. Dispose testers and avoid concurrent reuse of shared mutable fixtures.

## AI assistance

This package includes `skills/unittestex/SKILL.md`, with v7 API/generic testing, mocking, output and fixture guidance. Restore delivers the file to the NuGet cache; activation depends on the agent/tool. See [discovery and optional installation](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/ai-support.md). No build target installs instructions into your project.

## Documentation

- [API testing](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/api-testing.md)
- [Generic testing](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/generic-testing.md)
- [HTTP mocking](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/http-client-mocking.md)
- [Frameworks, fixtures and output](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/test-setup-and-extensibility.md)
- [Migrating from v5/v6 to v7](https://github.com/Avanade/UnitTestEx/blob/v7.0.0/docs/migration-v6.md)

Other packages: `UnitTestEx.Azure.Functions` for isolated-worker invocation, `UnitTestEx.Aspire` for real multi-process integration, and optional `UnitTestEx.Azure.Functions.Durable` for preview orchestration testing.
