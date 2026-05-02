# RazorStyle

| | CLI Tool | Build Package |
| --- | --- | --- |
| Version | [![PinguApps.RazorStyle.Cli version](https://img.shields.io/nuget/v/PinguApps.RazorStyle.Cli?style=for-the-badge&label=PinguApps.RazorStyle.Cli)](https://www.nuget.org/packages/PinguApps.RazorStyle.Cli/) | [![PinguApps.RazorStyle version](https://img.shields.io/nuget/v/PinguApps.RazorStyle?style=for-the-badge&label=PinguApps.RazorStyle)](https://www.nuget.org/packages/PinguApps.RazorStyle/) |
| Downloads | [![PinguApps.RazorStyle.Cli downloads](https://img.shields.io/nuget/dt/PinguApps.RazorStyle.Cli?style=for-the-badge&label=CLI%20downloads)](https://www.nuget.org/packages/PinguApps.RazorStyle.Cli/) | [![PinguApps.RazorStyle downloads](https://img.shields.io/nuget/dt/PinguApps.RazorStyle?style=for-the-badge&label=build%20package%20downloads)](https://www.nuget.org/packages/PinguApps.RazorStyle/) |

RazorStyle is an opinionated formatter and linter for Blazor `.razor` files. It exists to make component markup predictable across a codebase by enforcing a small set of layout and attribute-ordering rules that are easy to check in CI and easy to fix locally. The style is deliberately prescriptive: it favours consistency, readable diffs, and low-friction code review over preserving every author's preferred Razor layout.

## Contents

- [Packages](#packages)
- [Build Integration](#build-integration)
- [CLI Usage](#cli-usage)
- [Rules](#rules)
  - [RS0001 Attribute Wrapping](#rs0001-attribute-wrapping)
  - [RS0002 Child Content Lines](#rs0002-child-content-lines)
  - [RS0003 Attribute Order](#rs0003-attribute-order)

## Packages

RazorStyle is distributed in two forms:

- [`PinguApps.RazorStyle`](https://www.nuget.org/packages/PinguApps.RazorStyle/) is the NuGet build integration package. Use this when you want RazorStyle to run automatically as part of project builds. It is the best fit for shared enforcement because every developer and CI agent gets the same behaviour through `PackageReference`. By default, local builds fix files and CI builds check files when `ContinuousIntegrationBuild=true`. You can control whether it runs, whether it checks or fixes, and which rules are disabled through MSBuild properties.
- [`PinguApps.RazorStyle.Cli`](https://www.nuget.org/packages/PinguApps.RazorStyle.Cli/) is a .NET tool named `razorstyle`. Use this when you want an explicit command for local formatting, repository-wide checks, scripts, or CI jobs that should not depend on MSBuild. It is useful for one-off cleanup, pre-commit workflows, and direct control over the target path and disabled rules.

## Build Integration

Install the build package into a project that contains `.razor` files:

```powershell
dotnet add package PinguApps.RazorStyle
```

By default:

- local builds run `fix`
- CI builds run `check` when `ContinuousIntegrationBuild=true`
- `bin` and `obj` folders are excluded
- projects without `.razor` files are skipped

Override behaviour with MSBuild properties:

```xml
<PropertyGroup>
  <RazorStyleEnabled>true</RazorStyleEnabled>
  <RazorStyleCommand>check</RazorStyleCommand>
  <DisableRS0001>false</DisableRS0001>
  <DisableRS0002>false</DisableRS0002>
  <DisableRS0003>false</DisableRS0003>
</PropertyGroup>
```

Use the build package when the formatting rule should be part of the project contract. It keeps enforcement close to the project, works naturally in CI, and avoids relying on each developer remembering to run a separate tool command.

This repository sets `ContinuousIntegrationBuild=true` automatically when it detects GitHub Actions, Azure DevOps, a generic `CI=true` environment, TeamCity, or Jenkins. If your own consuming project does not already do that, add the same property in your build or CI configuration so the build package switches from `fix` to `check` during CI.

## CLI Usage

Install the tool:

```powershell
dotnet tool install --global PinguApps.RazorStyle.Cli
```

Check files:

```powershell
razorstyle check .\src
```

Fix files:

```powershell
razorstyle fix .\src
```

Disable specific rules for a run:

```powershell
razorstyle check .\src --disable RS0001 --disable RS0003
```

Use the CLI when you want direct control over when RazorStyle runs. It is a good fit for local cleanup, manual checks before opening a PR, custom CI steps, and scripts that should target a particular folder.

## Rules

- `RS0001`: start-tag attributes must wrap and align consistently.
- `RS0002`: child content must appear on its own line.
- `RS0003`: attributes must follow the preferred RazorStyle order.

### RS0001 Attribute Wrapping

`RS0001` keeps multi-attribute start tags readable by putting each additional attribute on its own aligned line. Tags with no attributes, or with a single attribute, stay inline so simple markup remains compact.

Before:

```razor
<Modal Title="Hello" IsOpen="true" OnClose="Close" />
```

After:

```razor
<Modal Title="Hello"
       IsOpen="true"
       OnClose="Close" />
```

Single-attribute and attribute-free tags remain inline:

```razor
<Modal />
<Modal Title="Hello" />
```

### RS0002 Child Content Lines

`RS0002` separates child content from the opening and closing tags. This makes inline element bodies easier to scan, gives nested markup room to breathe, and avoids small edits turning into noisy one-line diffs.

Before:

```razor
<span>Some text</span>
```

After:

```razor
<span>
    Some text
</span>
```

Self-closing tags are already valid:

```razor
<span />
<span class="foo" />
```

### RS0003 Attribute Order

`RS0003` applies a stable attribute order so the important identity, styling, binding, event, content, accessibility, and fallback attributes appear in a predictable place. In broad terms, unique identity-style attributes such as `@key`, `@ref`, `name`, and `id` come first, followed by `class` and `style`, binding and value-related attributes, event callbacks, common HTML/component attributes, templated or child-content attributes, general attributes, `aria-*`, `data-*`, splatted `@attributes`, and boolean or conditional attributes near the end.

Before:

```razor
<button data-track="save" disabled class="btn" @onclick="Save" id="save-button" />
```

After:

```razor
<button id="save-button"
        class="btn"
        @onclick="Save"
        data-track="save"
        disabled />
```
