# AI assistance and skill discovery

UnitTestEx packages include standard [Agent Skills](https://agentskills.io/specification) and package READMEs alongside their binaries. The guidance matches the package release and points to its tagged documentation rather than mutable `main` APIs.

Skills help an assistant choose the right test boundary, use supported APIs, and avoid known lifecycle and preview limitations. They are concise entry points, not another copy of the full documentation. Their usefulness and cost depend on the agent and task; package delivery alone does not establish accuracy or token savings.

## Available skills

Package | Skill | Use
-|-|-
`UnitTestEx` | `unittestex` | API/Generic Host testing, DI overrides, HTTP mocking, assertions, framework/output integration and fixtures.
`UnitTestEx.Azure.Functions` | `unittestex-azure-functions` | Normal application startup, isolated-worker invocation, middleware, trigger scopes and runtime proof boundaries.
`UnitTestEx.Aspire` | `unittestex-aspire` | Real multi-process resources, readiness, lifecycle hooks, logs, WireMock and async disposal.
`UnitTestEx.Azure.Functions.Durable` (preview) | `unittestex-durable` | SDK-host orchestration, opt-in activity/child discovery, activity mocks, virtual-time distinctions and preview limitations.

Each package contains `README.md` and `skills/<skill-name>/SKILL.md`. Source copies live beneath the corresponding `src` project; repository contributor instructions live separately in the root [`AGENTS.md`](../AGENTS.md).

## Restore is delivery, not activation

An ordinary `dotnet restore` puts the skills in the global NuGet package cache and records package files in the test project's `obj/project.assets.json`. UnitTestEx does **not** copy instructions into consumer repositories, import agent instructions through build targets or activate an agent feature.

Agent support and discovery tooling vary. You can point an assistant directly at the restored `SKILL.md`, copy a reviewed skill into your agent's supported location, or use optional discovery tooling.

To find your cache location without assuming the user's home directory:

```shell
dotnet nuget locals global-packages --list
```

Under that directory, for example, core v7 guidance is at `unittestex/7.0.0/skills/unittestex/SKILL.md`; Durable preview guidance is at `unittestex.azure.functions.durable/7.0.0-preview/skills/unittestex-durable/SKILL.md`. Read the version actually resolved by the consumer, not merely the newest cached version.

## Optional discovery tools

The [.NET team's NuGet skill delivery article](https://devblogs.microsoft.com/dotnet/delivering-skills-in-nuget-packages/) introduces [`dotnet-inspect`](https://www.nuget.org/packages/dotnet-inspect) and [`dotnet-package-skills`](https://www.nuget.org/packages/dotnet-package-skills). These are separate tools, not UnitTestEx dependencies. Install them only when wanted and consult their installed `--help` for version/selection options.

With `dotnet-inspect` available:

```shell
dotnet-inspect project MyTests.csproj -S Skills
dotnet-inspect package UnitTestEx -S Skills
dotnet-inspect package UnitTestEx.Azure.Functions -S Skills
dotnet-inspect package UnitTestEx.Aspire -S Skills
dotnet-inspect package UnitTestEx.Azure.Functions.Durable -S Skills
```

Replace `MyTests.csproj` with the actual test project. Package inspection may otherwise select a registry-default version; request the consumer's resolved version and prerelease selection as appropriate, or inspect its restored cache files directly. The new skills become available from the registry only once the corresponding packages are published.

If you explicitly want repository-local skills, from the consumer's project/solution directory:

```shell
dotnet-package-skills install
```

This command **writes files** into `.agents/skills`. Review the changes before committing; choose another supported location if your agent does not discover that directory. Do not have a build or restore run this automatically.

The tools described in the article default to scanning **direct package dependencies**. A specialized UnitTestEx package depends on core transitively; its skill does not duplicate all core guidance. If core's skill is absent, inspect the resolved core package/cache explicitly or use the tool's documented transitive-discovery option. Do not add a direct package reference merely to make a discovery tool see it.

## Keep guidance aligned

When package references change, refresh any copied skills, review the diff, and remove obsolete versions. Include only skills relevant to the task. Loading every guide into every prompt defeats progressive disclosure.

For contributors: update the owning skill/README when an API, supported framework, ownership rule or documented limitation changes. Keep examples aligned with repository-owned tests, keep tagged links aligned with the release, and inspect packed assets before publishing. Exercise representative API, Functions, Aspire and Durable tasks; distinguish runnable/example checks from actual with/without-skill agent evaluations. Neither is evidence of universal efficiency gains.
