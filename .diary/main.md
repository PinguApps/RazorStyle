## Rolling state
- Goal: Build RazorStyle as a standalone, packageable Razor formatter/linter.
- Current plan: Initial standalone implementation complete with organized Core/CLI folders, Spectre.Console.Cli tool, Build package, rule-disable switches, Reqnroll tests, and NuGet publish workflow.
- Open questions/risks: Version auto-incrementing is intentionally left for later; build package currently embeds net10.0 CLI binaries.
- Next actions: Review package metadata/naming, decide versioning strategy, then commit.
- Key paths: `src/PinguApps.RazorStyle.Core`, `src/PinguApps.RazorStyle.Cli`, `src/PinguApps.RazorStyle.Build`, `tests/PinguApps.RazorStyle.Tests`, `.github/workflows/publish.yml`

## Session log
### 2026-05-01 12:18 +01:00 (main)
- Scaffold standalone RazorStyle solution [build] (impact: high)
  - Why: Create first-class reusable packages separate from EnviroCrates.
  - Change: Added `.slnx`, CPM, shared build props/targets, `.editorconfig`, `AGENTS.md`, and `global.json` (files: `PinguApps.RazorStyle.slnx`, `Directory.*.props`, `Directory.Build.targets`, `.editorconfig`, `AGENTS.md`, `global.json`)
- Port RazorStyle engine [tools] (impact: high)
  - Why: Reuse proven Razor scanner/rules/fixers in standalone namespace.
  - Change: Added Core project with RS0001/RS0002/RS0003, text scanner, runner, replacements, and file IO helpers (files: `src/PinguApps.RazorStyle.Core/*`)
- Add distribution packages [packaging] (impact: high)
  - Why: Provide both manual CLI usage and drop-in PackageReference build integration.
  - Change: Added `PinguApps.RazorStyle.Cli` .NET tool with Spectre.Console output and `PinguApps.RazorStyle` build package embedding CLI binaries (files: `src/PinguApps.RazorStyle.Cli/*`, `src/PinguApps.RazorStyle.Build/*`)
- Add tests and publishing [tests] (impact: med)
  - Why: Keep behavior covered and publish packages on main merges.
  - Change: Added Reqnroll tests, README, and NuGet publish workflow using `NUGET_API_KEY` (files: `tests/PinguApps.RazorStyle.Tests/*`, `README.md`, `.github/workflows/publish.yml`)
- Verify packages [build] (impact: low)
  - Why: Confirm restore/build/test/pack and both package usage paths work.
  - Change: Release restore/build/test/pack passed; local tool install passed; PackageReference smoke fixed Razor locally and CI-mode check failed as expected (cmds: `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet pack`, `dotnet tool install`, smoke builds)
### 2026-05-01 17:05 +01:00 (main)
- Add rule disable switches [tools] (impact: med)
  - Why: Allow consumers to disable specific rules from MSBuild properties such as `<DisableRS0001>true</DisableRS0001>`.
  - Change: Added `RazorStyleOptions`, runner filtering, CLI `--disable <rule>` flags, build props/targets mapping for `DisableRS0001`/`DisableRS0002`/`DisableRS0003`, README docs, and Reqnroll coverage (files: `src/PinguApps.RazorStyle.Core/*`, `src/PinguApps.RazorStyle.Cli/Program.cs`, `src/PinguApps.RazorStyle.Build/buildTransitive/*`, `README.md`, `tests/PinguApps.RazorStyle.Tests/*`)
- Verify disables [build] (impact: low)
  - Why: Confirm disabled rules are not applied from both tests and PackageReference build integration.
  - Change: Release build/test/pack passed; PackageReference smoke with disabled rules passed in CI-mode check (cmds: `dotnet build PinguApps.RazorStyle.slnx -c Release`, `dotnet test PinguApps.RazorStyle.slnx -c Release --no-build`, `dotnet pack PinguApps.RazorStyle.slnx -c Release --no-build -o artifacts/packages`)
### 2026-05-01 17:31 +01:00 (main)
- Upgrade CLI command model [cli] (impact: med)
  - Why: Use Spectre.Console for argument parsing/help/examples and remove locale-sensitive `MarkupLine` warnings.
  - Change: Added `Spectre.Console.Cli`, replaced manual top-level parsing with `CommandApp`, `CheckCommand`, `FixCommand`, shared settings, and invariant-culture output (files: `src/PinguApps.RazorStyle.Cli/*`, `Directory.Packages.props`)
- Verify Spectre CLI [build] (impact: low)
  - Why: Confirm generated help, repeated `--disable`, packaging, and embedded build package still work.
  - Change: Release build/test/pack passed; CLI help and PackageReference smoke passed (cmds: `dotnet build PinguApps.RazorStyle.slnx -c Release`, `dotnet test PinguApps.RazorStyle.slnx -c Release --no-build`, `dotnet pack PinguApps.RazorStyle.slnx -c Release --no-build -o artifacts/packages`, `dotnet run --project src/PinguApps.RazorStyle.Cli -- fix --help`)
### 2026-05-01 17:36 +01:00 (main)
- Organize project files [tools] (impact: med)
  - Why: Keep Core and CLI files in responsibility-based folders instead of project roots.
  - Change: Moved Core files into `Diagnostics`, `Documents`, `Parsing`, `Rules`, `Formatting`, and `Running`; moved CLI command/runner files into `Commands` and `Infrastructure`; updated namespaces/global usings (files: `src/PinguApps.RazorStyle.Core/*`, `src/PinguApps.RazorStyle.Cli/*`)
- Update README examples [docs] (impact: low)
  - Why: Position the package as an opinionated Razor style enforcer and show before/after examples for every rule.
  - Change: Added RS0001/RS0002/RS0003 examples and removed the limitations section (files: `README.md`)
- Verify restructure [build] (impact: low)
  - Why: Confirm namespace moves, formatting, tests, and package output still work.
  - Change: Release build/test/pack passed; whitespace verification passed (cmds: `dotnet build PinguApps.RazorStyle.slnx -c Release`, `dotnet format PinguApps.RazorStyle.slnx whitespace --verify-no-changes --verbosity minimal`, `dotnet test PinguApps.RazorStyle.slnx -c Release --no-build`, `dotnet pack PinguApps.RazorStyle.slnx -c Release --no-build -o artifacts/packages`)
