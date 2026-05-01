# AGENTS.md

## WORK DIARY

### Purpose
- Keep a small, high-signal diary so future sessions can resume quickly.

### When to read/write
- On session start: read the diary file (if it exists) to regain context.
- Once per response (just before replying): update the diary ONLY if you took meaningful actions (code/config changes, important commands run, decisions made, constraints/bugs discovered, tasks created that affect next steps). Otherwise: do not write.

### Location
- Always read/write inside `.diary/`

### Filename (from git branch)
- If branch is `vk/<suffix>` or `feature/<suffix>` → file is `.diary/<suffix>.md`
  - e.g. `vk/ab12-foo-bar` → `.diary/ab12-foo-bar.md`
- The branch should always have a prefix, but the prefix cannot be guaranteed, just use the suffix in every case after the `/`.

### Format (Markdown, compact)
- The file has two sections:

1) Rolling state (edit in place; keep ≤12 bullets total)
```
## Rolling state
- Goal: <one sentence>
- Current plan: <1–3 bullets>
- Open questions/risks: <0–3 bullets>
- Next actions: <1–5 bullets>
- Key paths: <optional; 1–5 entries>
```

2) Session log (append-only; per response keep ≤5 bullets)
```
## Session log
### <YYYY-MM-DD HH:MM Z> (<branch>)
- <Verb + object> [area] (impact: none|low|med|high)
  - Why: <reason/decision>
  - Change: <what changed> (files: <a,b,c> | cmds: `<...>`)
  - Notes: <gotchas/follow-ups> (optional)
```

### Compression rules
- Prefer deltas over narration (Add/Remove/Refactor/Fix).
- Use short tags for area: [ui] [api] [db] [auth] [infra] [build] [tests] etc...
- Include "Why" for any non-obvious decision and "Notes" for any caveat.
- Do not exceed caps; omit low-value detail.

## Operating Principles

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.


## Project Context

This repository contains RazorStyle, a standalone Razor formatting and linting tool for Blazor `.razor` files.

Projects:
- `src/PinguApps.RazorStyle.Core`: parsing, diagnostics, rules, and fix logic.
- `src/PinguApps.RazorStyle.Cli`: .NET tool entry point.
- `src/PinguApps.RazorStyle.Build`: NuGet build integration package.
- `tests/PinguApps.RazorStyle.Tests`: Reqnroll-driven behavior tests.

## Repository Conventions

- Use the .NET SDK pinned in `global.json`.
- Use `.slnx`, not `.sln`.
- Use central package management through `Directory.Packages.props`.
- Keep production code under `src/` and test code under `tests/`.
- Keep project files minimal; prefer shared settings in `Directory.Build.props` and `Directory.Build.targets`.
- Keep one class, record, enum, or interface per file.
- Follow `.editorconfig` and repository-wide MSBuild settings.
- Do not use primary constructors in classes. Records may use primary constructors.

## Testing

- Tests should be Reqnroll-driven where behavior is being specified.
- Prefer business-readable feature files and focused step definitions.
- A change is complete only when restore, build, tests, and pack pass locally where relevant.

## Packaging

- `PinguApps.RazorStyle.Cli` is packaged as a .NET tool for manual use.
- `PinguApps.RazorStyle` is the build integration NuGet package.
- The build package should be installable via `PackageReference` and should invoke the embedded RazorStyle CLI during build.
