## Rolling state
- Goal: Refresh README structure and CI/build package guidance for RazorStyle.
- Current plan: Complete README refresh and broaden repo CI detection.
- Open questions/risks: Badge rendering depends on shields.io and NuGet package metadata availability.
- Next actions: Review final README in rendered Markdown if desired.
- Key paths: README.md, Directory.Build.props

## Session log
### 2026-05-02 03:22 +01:00 (feature/docs-refresh)
- Refresh README [docs] (impact: low)
  - Why: User requested badges, contents, clearer package guidance, build integration before CLI, and richer rule descriptions.
  - Change: Rewrote README structure with NuGet version/download badges, contents, package choice guidance, build integration, CLI usage, and rule descriptions. (files: README.md | cmds: `git diff --check`)
  - Notes: No production or test code changed.
### 2026-05-02 03:32 +01:00 (feature/docs-refresh)
- Broaden CI detection [build] (impact: low)
  - Why: Azure DevOps should set CI mode through `TF_BUILD`, with reasonable support for other common CI hosts.
  - Change: `ContinuousIntegrationBuild` now auto-enables for GitHub Actions, Azure DevOps, generic `CI=true`, TeamCity, and Jenkins unless explicitly set; README documents the behaviour. (files: Directory.Build.props, README.md | cmds: `dotnet msbuild ... -getProperty:ContinuousIntegrationBuild`, `dotnet build PinguApps.RazorStyle.slnx --no-restore`, `git diff --check`)
