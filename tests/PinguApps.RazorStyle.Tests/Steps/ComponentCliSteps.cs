using System.Diagnostics;
using Reqnroll;
using Xunit;

namespace PinguApps.RazorStyle.Tests.Steps;

[Binding]
public sealed class ComponentCliSteps : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "razorstyle-cli-" + Guid.NewGuid().ToString("N"));
    private string _arguments = string.Empty;
    private string _output = string.Empty;
    private int _exitCode;

    [Given("a restored component project for the CLI")]
    public async Task GivenProject()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "global.json"), """{"sdk":{"version":"10.0.403","rollForward":"latestPatch"}}""");
        await File.WriteAllTextAsync(Path.Combine(_directory, "Fixture.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
              <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(_directory, "Example.razor"), "<div />");
        await File.WriteAllTextAsync(Path.Combine(_directory, "Example.razor.cs"), """
            using Microsoft.AspNetCore.Components;
            namespace Fixture;
            public partial class Example
            {
                private void Work() { }
                [Parameter] public int Value { get; set; }
                private class Nested { }
            }
            public class Ordinary
            {
                public void Work() { }
                public int Value;
                private class Nested { }
            }
            """);
        (int code, string output) = await RunAsync(["restore", Path.Combine(_directory, "Fixture.csproj"), "--disable-build-servers", "--nologo", "-v:q"]);
        Assert.True(code == 0, output);
    }

    [Given("the CLI project sets {string} to true")]
    public async Task GivenDisabled(string property)
    {
        string file = Path.Combine(_directory, "Fixture.csproj");
        string text = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, text.Replace("</PropertyGroup>", "<" + property + ">true</" + property + "></PropertyGroup>", StringComparison.Ordinal));
    }

    [Given("the CLI arguments are {string}")]
    public void GivenArguments(string arguments) => _arguments = arguments;

    [Given("the CLI component is in an ordinary C# file")]
    public void GivenOrdinaryFilename() => File.Move(Path.Combine(_directory, "Example.razor.cs"), Path.Combine(_directory, "Example.cs"));

    [Given("the CLI component has no project")]
    public void GivenNoProject() => File.Delete(Path.Combine(_directory, "Fixture.csproj"));

    [Given("the CLI component has a compiler error")]
    public async Task GivenCompilerError()
    {
        string file = Path.Combine(_directory, "Example.razor.cs");
        string text = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, text.Replace("private void Work() { }", "private void Work() { Missing(); }", StringComparison.Ordinal));
    }

    [Given("the CLI project suppresses component rules in editorconfig")]
    public Task GivenEditorConfig() => File.WriteAllTextAsync(Path.Combine(_directory, ".editorconfig"), """
        root = true
        [*.cs]
        dotnet_diagnostic.PARS0004.severity = none
        dotnet_diagnostic.PARS0005.severity = none
        """);

    [When("RazorStyle checks the component project")]
    public async Task WhenChecked()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PinguApps.RazorStyle.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string cli = Path.Combine(root.FullName, "src", "PinguApps.RazorStyle.Cli", "bin", configuration, "net10.0", "PinguApps.RazorStyle.Cli.dll");
        List<string> arguments = [cli, "check", _directory];
        arguments.AddRange(_arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        (_exitCode, _output) = await RunAsync(arguments);
    }

    [Then("the CLI exits with {int}")]
    public void ThenExit(int expected) => Assert.True(_exitCode == expected, _output);

    [Then("the CLI reports {string}")]
    public void ThenReports(string id) => Assert.Contains(id, _output, StringComparison.Ordinal);

    [Then("the CLI does not report {string}")]
    public void ThenNotReports(string id) => Assert.DoesNotContain(id, _output, StringComparison.Ordinal);

    private async Task<(int Code, string Output)> RunAsync(IEnumerable<string> arguments)
    {
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? Path.Combine(Directory.GetParent(runtime)!.Parent!.Parent!.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        ProcessStartInfo start = new(dotnet)
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        foreach (string name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DOTNET_MSBUILD_SDK_RESOLVER_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(name);
        }
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout + await stderr);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
