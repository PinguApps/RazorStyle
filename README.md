# RazorStyle

Component code-behind conventions, compiler enforcement and safe editor fixes
are described in [Component members](https://github.com/PinguApps/RazorStyle/blob/main/docs/component-members.md).

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
  - [PARS0001 Attribute Wrapping](#pars0001-attribute-wrapping)
  - [PARS0002 Child Content Lines](#pars0002-child-content-lines)
  - [PARS0003 Attribute Order](#pars0003-attribute-order)

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
  <DisablePARS0001>false</DisablePARS0001>
  <DisablePARS0002>false</DisablePARS0002>
  <DisablePARS0003>false</DisablePARS0003>
  <DisablePARS0004>false</DisablePARS0004>
  <DisablePARS0005>false</DisablePARS0005>
  <RazorStylePreserveAttributeSplattingOrder>false</RazorStylePreserveAttributeSplattingOrder>
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

Component checks load the actual C# projects and references, so restore those
projects first. They apply to authored partial classes whose inheritance chain
reaches `ComponentBase`, whether in `.razor.cs` or another `.cs` file. Project
paths and individual component files are also supported. See
[component checks and switches](https://github.com/PinguApps/RazorStyle/blob/main/docs/component-members.md#standalone-cli-check).

Fix files:

```powershell
razorstyle fix .\src
```

The `fix` command applies supported markup fixes. Use the editor ordering
action or Fix All for safe C# member reordering; nested-type extraction is manual.

Disable specific rules for a run:

```powershell
razorstyle check .\src --disable PARS0001 --disable PARS0003
```

Opt into preserving attribute precedence around `@attributes` for either command:

```powershell
razorstyle fix .\src --preserve-attribute-splatting-order
razorstyle check .\src --preserve-attribute-splatting-order
```

Use the CLI when you want direct control over when RazorStyle runs. It is a good fit for local cleanup, manual checks before opening a PR, custom CI steps, and scripts that should target a particular folder.

## Rules

- `PARS0001`: start-tag attributes must wrap and align consistently.
- `PARS0002`: child content must appear on its own line.
- `PARS0003`: attributes must follow the preferred RazorStyle order.
- `PARS0004`: component members must follow the approved order, including binding pairs and lifecycle hooks.
- `PARS0005`: component code-behinds must not contain nested types.

### PARS0001 Attribute Wrapping

`PARS0001` keeps multi-attribute start tags readable by putting each additional attribute on its own aligned line. Tags with no attributes, or with a single attribute, stay inline so simple markup remains compact.

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

### PARS0002 Child Content Lines

`PARS0002` separates child content from the opening and closing tags. This makes inline element bodies easier to scan, gives nested markup room to breathe, and avoids small edits turning into noisy one-line diffs.

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

### PARS0003 Attribute Order

`PARS0003` applies a stable attribute order so the important identity, styling, binding, event, content, accessibility, and fallback attributes appear in a predictable place. In broad terms, unique identity-style attributes such as `@key`, `@ref`, `name`, and `id` come first, followed by `class` and `style`, binding and value-related attributes, event callbacks, common HTML/component attributes, templated or child-content attributes, general attributes, `aria-*`, `data-*`, splatted `@attributes`, and boolean or conditional attributes near the end.

Each ordering diagnostic names the offending attribute and lists the exact
expected sequence for that tag, for example `id -> class -> data-track`, so you
can correct it directly. With splatting preservation enabled, that sequence
retains the `@attributes` boundary.

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

Attribute splatting order is preserved only when you opt in. Set this property in your `.csproj` or `Directory.Build.props`:

```xml
<PropertyGroup>
  <RazorStylePreserveAttributeSplattingOrder>true</RazorStylePreserveAttributeSplattingOrder>
</PropertyGroup>
```

With this option enabled, `@attributes` stays in its existing position. The same ordering rule sorts each group of attributes before and after it independently, without moving any attribute across it. This preserves which explicit attributes override values in the splatted dictionary. Both `check` and `fix` use this behaviour, and attribute wrapping still applies.

For example, ignoring wrapping:

```razor
<!-- Before -->
<div title="Hello" @attributes="Extras" class="card" id="item" />

<!-- After -->
<div title="Hello" @attributes="Extras" id="item" class="card" />
```

The property defaults to `false`, retaining the global ordering described above. `DisablePARS0003=true` still disables attribute ordering entirely, even when this option is enabled.

### PARS0004 Component Member Order

Both packages enforce the same order below: `PinguApps.RazorStyle` reports it
during compilation, and `PinguApps.RazorStyle.Cli` reports it through
`razorstyle check`. It applies to authored **partial component classes whose
inheritance chain reaches the framework `ComponentBase`**, including through
intermediate base classes. A `.razor.cs` filename is conventional, but a component
partial declaration in another `.cs` file is checked too. Non-component classes
and Razor-generated C# are excluded.

Put the members present in **each partial declaration** in this exact order:

1. **Cascading parameter properties**, regardless of their type or other attributes.
2. **Editor-required parameter groups**: each `[Parameter, EditorRequired]`
   value immediately followed by its associated `XChanged` callback, if present.
3. **Optional parameter groups**: each remaining value parameter immediately
   followed by its associated `XChanged` callback, if present.
4. **Standalone callback parameters**: `EventCallback` / `EventCallback<T>`
   parameters without an associated value parameter, such as `OnSaved`.
5. **Content parameters**: `RenderFragment` and `RenderFragment<T>`.
6. **Constants and fields**: static, readonly, injected dependencies, mutable
   state, and component/element reference fields all share this group.
7. **Constructors**.
8. **Other properties**, including explicit-interface properties.
9. **Ordinary .NET events**, including field-like events and custom/explicit-interface accessors.
10. **Lifecycle overrides**, in the exact sequence below.
11. **All other methods**, including helpers, handlers and `SetParametersAsync`.
12. **Disposal implementations**: both `IDisposable.Dispose` and
    `IAsyncDisposable.DisposeAsync`, including explicit-interface implementations.

Within group 10, the lifecycle sequence is:

1. `OnInitialized()`
2. `OnInitializedAsync()`
3. `OnParametersSet()`
4. `OnParametersSetAsync()`
5. `ShouldRender()`
6. `OnAfterRender(bool firstRender)`
7. `OnAfterRenderAsync(bool firstRender)`

Only actual framework overrides belong to that sequence. Same-named helpers
or overloads stay in the ordinary-method group. Missing groups and hooks are
allowed; **do not add empty members just to fill the list**. Keep the existing
relative order within every group, except for binding adjacency and lifecycle
ordering. There is no alphabetical, visibility, static/instance or ordinary
sync/async ordering. If both disposal methods exist, keep their relative order
and place both below ordinary methods.

**Binding pairs stay together.** Both `X` and `XChanged` must be actual framework
`[Parameter]` properties. The callback must be a compatible framework
`EventCallback<T>` with matching value type (including generic/nullable types),
or an untyped `EventCallback`. A `Changed` suffix alone does not establish a pair.
The pair takes its required/optional position from **X**; `XChanged` does not need
`EditorRequired`. Thus required `Title`/`TitleChanged`, required
`Value`/`ValueChanged`, then optional `Disabled`/`DisabledChanged` is valid.
Moving a value parameter must keep its callback immediately after it.

Cascading parameters always win classification. Otherwise, an editor-required
parameter belongs in group 2 even if its type is callback/content. For optional
parameters, a recognised binding pair wins over standalone callback/content
classification.

This complete example demonstrates the sequence; omit members your component
does not need:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

public partial class ExampleControl : ComponentBase, IDisposable, IAsyncDisposable
{
    [CascadingParameter]
    public EditContext? EditContext { get; set; }

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;
    [Parameter]
    public EventCallback<string> TitleChanged { get; set; }

    [Parameter, EditorRequired]
    public int Value { get; set; }
    [Parameter]
    public EventCallback<int> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }
    [Parameter]
    public EventCallback<bool> DisabledChanged { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    [Parameter]
    public RenderFragment<int>? ItemTemplate { get; set; }

    private int _saveCount;

    public ExampleControl() { }

    public int SaveCount => _saveCount;

    public event Action? Saved;

    protected override void OnInitialized() { }
    protected override Task OnInitializedAsync() => Task.CompletedTask;
    protected override void OnParametersSet() { }
    protected override Task OnParametersSetAsync() => Task.CompletedTask;
    protected override bool ShouldRender() => true;
    protected override void OnAfterRender(bool firstRender) { }
    protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

    private void Save()
    {
        _saveCount++;
        Saved?.Invoke();
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

An ordering diagnostic names the first correction, for example
`Move 'int Example.Value' before 'void Example.Work()'.`, and includes all twelve groups,
the lifecycle sequence and the binding-pair requirement in the message itself.
After correcting that position, recheck for any remaining violations. For a pair
split across partial declarations, the diagnostic names both partners, identifies
the other location and explains that manual movement is required.

The editor's **Order component members** action and **Fix All** reorder safe
declarations. They preserve attributes, documentation, comments and executable
code. No automatic ordering fix is offered when it would change the relative
order of initializers, cross directives/regions or partial declarations, or
when the declaration has compiler errors or nested types. Fix those cases
manually while preserving behaviour. `razorstyle fix` remains markup-only.

### PARS0005 No Nested Component Types

The same component scope must not contain nested classes, structs, records,
enums, interfaces or delegates, at any visibility. Move each type to its own
top-level file in the appropriate namespace. For example, a component's nested
`Item` record belongs in `Item.cs`, and its `SaveState` enum in `SaveState.cs`.
Top-level types cannot be private; preserve accessibility, references, public
API and generic context deliberately. The diagnostic names the forbidden type
and explains the extraction, and no unsafe extraction fix is offered.

Disable either component rule using `<DisablePARS0004>true</DisablePARS0004>` or
`<DisablePARS0005>true</DisablePARS0005>` in a `.csproj`/`Directory.Build.props`,
or `--disable PARS0004` / `--disable PARS0005` for a CLI check. Standard Roslyn
suppression also applies. See the [component guide](https://github.com/PinguApps/RazorStyle/blob/main/docs/component-members.md)
for build severity configuration and standalone project-loading requirements.
