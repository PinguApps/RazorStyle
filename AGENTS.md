# AGENTS.md

## Work Diary

### Purpose
- Keep a small, high-signal diary so future sessions can resume quickly.

### When To Read/Write
- On session start: read the diary file in `.diary/` if it exists.
- Once per response, just before replying: update the diary only if meaningful actions were taken.

### Location
- Always read/write inside `.diary/`.

### Filename
- If the branch has a slash, use the suffix after `/`.
- Otherwise use the branch name.

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
