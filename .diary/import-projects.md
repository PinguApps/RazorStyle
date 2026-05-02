## Rolling state
- Goal: Address PR #1 review feedback on rule fix behaviour.
- Current plan: Done; code-block and Razor-expression review comments have local fixes, regression coverage, and GitHub replies.
- Open questions/risks: Changes are local workspace edits and have not been committed or pushed.
- Next actions: Commit/push if the PR branch should be updated from this workspace.
- Key paths: src/PinguApps.RazorStyle.Core/Parsing/RazorTagScanner.cs, src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature

## Session log
### 2026-05-01 23:49 +01:00 (feature/import-projects)
- Fix CLI diagnostic output [build] (impact: med)
  - Why: Spectre markup can wrap long warning-format diagnostic lines, causing MSBuild to parse only the first physical line.
  - Change: Write diagnostics with `Console.Out.WriteLine` while leaving summaries/errors on Spectre. (files: src/PinguApps.RazorStyle.Cli/Infrastructure/RazorStyleCliRunner.cs | cmds: `dotnet build PinguApps.RazorStyle.slnx`, `dotnet test PinguApps.RazorStyle.slnx --no-build`, `dotnet pack src\PinguApps.RazorStyle.Build\PinguApps.RazorStyle.Build.csproj`)
  - Notes: `dotnet pack --no-build` was rerun without `--no-build` so the package includes the rebuilt CLI.
### 2026-05-02 00:04 +01:00 (feature/import-projects)
- Inspect ignored diary files [git] (impact: low)
  - Why: Confirm why `.diary/` files are not visible to Git despite not appearing in tracked ignore files.
  - Change: Found `.diary/` in local Git exclude. (files: .git/info/exclude | cmds: `git status --short --ignored .diary`, `git check-ignore -v .diary/*`, `git ls-files .diary`)
### 2026-05-02 00:04 +01:00 (feature/import-projects)
- Review branch diff [tests] (impact: low)
  - Why: User requested review against merge base `aa807fc9d02da5a70fa0a4c05b0d77428fc09d87`.
  - Change: Inspected diff, packed build package, listed package contents, and ran build/tests. (files: none | cmds: `git diff aa807fc9d02da5a70fa0a4c05b0d77428fc09d87`, `dotnet pack src\PinguApps.RazorStyle.Build\PinguApps.RazorStyle.Build.csproj -c Release -o artifacts\review-pack`, `tar -tf artifacts\review-pack\PinguApps.RazorStyle.0.1.0.nupkg`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`)
  - Notes: Found RS0002 misses tags containing self-closing child tags with the same name.
### 2026-05-02 00:10 +01:00 (feature/import-projects)
- Fix RS0002 same-name child matching [rules] (impact: med)
  - Why: `<Panel><Panel /></Panel>` skipped RS0002 because the self-closing child increased same-name nesting depth.
  - Change: Added same-name start-tag parsing that ignores self-closing children and checks tag-name boundaries; added Reqnroll scenario. (files: src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature | cmds: `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`)
### 2026-05-02 00:25 +01:00 (feature/import-projects)
- Fix PR review comments [rules] (impact: med)
  - Why: PR #1 feedback found RS0003 applying RS0001 wrapping when disabled, and RS0002 accepting prefix closing-tag matches.
  - Change: RS0003 now reorders attributes in existing slots; RS0002 closing-tag detection checks tag-name boundaries; added two Reqnroll scenarios and replied on both GitHub threads. (files: src/PinguApps.RazorStyle.Core/Rules/AttributeOrderRule.cs, src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature | cmds: `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet pack src\PinguApps.RazorStyle.Build\PinguApps.RazorStyle.Build.csproj -c Release --no-restore`)
  - Notes: Edits remain local; GitHub replies explicitly say "Addressed locally."
### 2026-05-02 00:31 +01:00 (feature/import-projects)
- Correct RS0002 nested formatting regression [rules] (impact: med)
  - Why: The new prefix-closing regression expected under-indented nested content.
  - Change: Updated expected output and made RS0002 account for parent-introduced indentation when rewriting nested inline content. (files: src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature | cmds: `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`)
### 2026-05-02 00:47 +01:00 (feature/import-projects)
- Fix raw-text review comments [rules] (impact: med)
  - Why: PR #1 feedback found raw-text closing strings could terminate parent matching or raw-text scanner skips.
  - Change: Scanner and RS0002 now require full raw-text closing tag-name matches; RS0002 skips script/style bodies while finding parent closes; added two Reqnroll scenarios and replied on both GitHub threads. (files: src/PinguApps.RazorStyle.Core/Parsing/RazorTagScanner.cs, src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature, tests/PinguApps.RazorStyle.Tests/Steps/RazorStyleRulesSteps.cs | cmds: `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet pack src\PinguApps.RazorStyle.Build\PinguApps.RazorStyle.Build.csproj -c Release --no-restore`)
  - Notes: Edits remain local; GitHub replies explicitly say "Addressed locally."
### 2026-05-02 01:06 +01:00 (feature/import-projects)
- Fix code-region review comments [rules] (impact: med)
  - Why: Temporary regressions confirmed scanner parsed tags inside `@code` strings and RS0002 matched close text inside quoted Razor expressions.
  - Change: Scanner skips Razor comments plus `@code`/`@functions` blocks; RS0002 skips explicit Razor expressions during close-tag search; kept both regression scenarios and replied on GitHub threads. (files: src/PinguApps.RazorStyle.Core/Parsing/RazorTagScanner.cs, src/PinguApps.RazorStyle.Core/Rules/ChildContentLineRule.cs, tests/PinguApps.RazorStyle.Tests/Features/RazorStyleRules.feature | cmds: `dotnet test PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet build PinguApps.RazorStyle.slnx -c Release --no-restore`, `dotnet pack src\PinguApps.RazorStyle.Build\PinguApps.RazorStyle.Build.csproj -c Release --no-restore`, `git diff --check`)
