# Component code-behind conventions

The `PinguApps.RazorStyle` build package includes Roslyn diagnostics for authored
partial classes deriving from the framework `ComponentBase`, including generic
components and inherited component bases. Generated Razor C# and ordinary C#
classes are excluded. Each partial declaration is checked independently.

`PARS0004` orders members. `PARS0005` prohibits nested classes, structs, records,
enums, interfaces and delegates, regardless of accessibility. Extract nested
types manually to individual files while preserving references, accessibility
and generic context. There is no automatic extraction fix.

The member groups, in order, are:

1. Cascading parameters.
2. Editor-required parameter groups.
3. Optional parameter groups.
4. Standalone callback parameters.
5. Content parameters (`RenderFragment` and `RenderFragment<T>`).
6. Constants and fields.
7. Constructors.
8. Other properties.
9. .NET events, including custom accessors.
10. Lifecycle overrides: `OnInitialized`, `OnInitializedAsync`, `OnParametersSet`,
    `OnParametersSetAsync`, `ShouldRender`, `OnAfterRender`, `OnAfterRenderAsync`.
11. Other methods, including `SetParametersAsync`.
12. Both `IDisposable.Dispose` and `IAsyncDisposable.DisposeAsync` implementations.

Omitted groups and hooks are allowed. Relative order within a group is retained,
including the relative order of Dispose and DisposeAsync. Ordinary helpers,
handlers and sync/async methods have no additional ordering. Lifecycle identity
comes from actual overrides, and disposal identity comes from interface
implementation; similarly named helpers remain ordinary methods.

A binding group consists of parameter `X` followed immediately by parameter
`XChanged` with framework `EventCallback<T>` whose `T` matches `X`'s type,
including generic and nullable types. The untyped framework `EventCallback`
also supports a binding value of any resolved type. Associations requiring conversions or
unresolved types are not guessed. The pair takes its required/optional rank
from `X`; the callback does not need `EditorRequired`. Cascading parameters
always come first. Required parameters take precedence over callback/content
classification; optional binding groups take precedence over those groups too.
Aliases, qualified attribute names and `Attribute` suffixes are supported.

The **Order component members** editor fix supports Roslyn Fix All. It moves
attributes, documentation and attached comments with members, retaining bodies
and signatures. It withholds the fix when the declaration contains syntax
or compiler errors, nested types, directives (including regions), a binding partner in
another partial declaration, or a change in the relative order of initialized
fields/properties. Diagnostics still explain what needs manual attention. A
cross-part binding diagnostic includes the partner location. No members are
generated or transferred between files.

## Build enforcement

Install the existing package using `PackageReference`. Roslyn analyzes authored
partial component classes (typically `.razor.cs`); the existing markup check remains separate.
Use standard `.editorconfig` severities to enforce these warnings as errors:

```ini
[*.cs]
dotnet_diagnostic.PARS0004.severity = error
dotnet_diagnostic.PARS0005.severity = error
```

Standard Roslyn suppression (`#pragma warning disable`, `NoWarn`, or severity
`none`) applies. These rules have no configurable ordering scheme. The markup
CLI's `--disable` options apply to markup rules, not these compiler diagnostics.

## Complete example

`ExampleControl.razor.cs`:

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
    public RenderFragment<Item>? ItemTemplate { get; set; }

    private SaveState _state;

    public ExampleControl() { }

    public bool IsSaving => _state == SaveState.Saving;

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
        _state = SaveState.Saving;
        Saved?.Invoke();
        _state = SaveState.Idle;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

`Item.cs`:

```csharp
public sealed record Item(int Id, string Label);
```

`SaveState.cs`:

```csharp
internal enum SaveState { Idle, Saving }
```
