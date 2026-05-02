# RazorStyle

RazorStyle is an opinionated Razor style linter and enforcer for Blazor `.razor` files. It exists to make Razor markup consistent across a solution, either by checking files in CI or fixing files during local builds.

It is distributed as two packages:

- `PinguApps.RazorStyle.Cli`: a .NET tool named `razorstyle`.
- `PinguApps.RazorStyle`: an MSBuild integration package that runs RazorStyle during builds.

## Rules

- `RS0001`: start-tag attributes must wrap and align consistently.
- `RS0002`: child content must appear on its own line.
- `RS0003`: attributes must follow the preferred RazorStyle order.

### RS0001 Attribute Wrapping

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

## Build Integration

Install the build package into a project that contains `.razor` files:

```powershell
dotnet add package PinguApps.RazorStyle
```

By default:

- local builds run `fix`
- CI builds run `check` when `ContinuousIntegrationBuild=true`

Override behavior with MSBuild properties:

```xml
<PropertyGroup>
  <RazorStyleEnabled>true</RazorStyleEnabled>
  <RazorStyleCommand>check</RazorStyleCommand>
  <DisableRS0001>false</DisableRS0001>
  <DisableRS0002>false</DisableRS0002>
  <DisableRS0003>false</DisableRS0003>
</PropertyGroup>
```

The CLI also supports per-rule disables:

```powershell
razorstyle check .\src --disable RS0001 --disable RS0003
```
