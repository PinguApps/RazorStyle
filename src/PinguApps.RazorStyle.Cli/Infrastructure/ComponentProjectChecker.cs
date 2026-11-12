using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using PinguApps.RazorStyle.CodeAnalysis;

namespace PinguApps.RazorStyle.Cli.Infrastructure;

internal static class ComponentProjectChecker
{
    public static async Task<IReadOnlyList<RazorDiagnostic>> CheckAsync(string path, RazorStyleOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath)! : fullPath;
        string[] projects = File.Exists(fullPath) && Path.GetExtension(fullPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            ? [fullPath] : FindProjects(directory);
        if (projects.Length == 0)
        {
            // Markup-only folders need no C# project. Authored code-behinds must not silently pass without semantic context.
            return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Any(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj" or "node_modules" or ".git"))
                ? throw new InvalidOperationException("No C# project found for the code-behind files. Check their containing .csproj or project directory.")
                : [];
        }

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterInstance(MSBuildLocator.QueryVisualStudioInstances(
                new VisualStudioInstanceQueryOptions { WorkingDirectory = directory, DiscoveryTypes = DiscoveryType.DotNetSdk })
                .FirstOrDefault() ?? throw new InvalidOperationException("No compatible .NET SDK was found. Install the SDK selected by the consumer's global.json."));
        }

        return await CheckProjectsAsync(projects, fullPath, options).ConfigureAwait(false);
    }

    private static string[] FindProjects(string directory)
    {
        string[] projects = [.. Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj" or "node_modules" or ".git"))
            .Order(StringComparer.Ordinal)];
        if (projects.Length != 0)
        {
            return projects;
        }

        // A file or component subfolder uses its nearest containing project(s).
        for (DirectoryInfo? parent = Directory.GetParent(directory); parent is not null; parent = parent.Parent)
        {
            projects = Directory.GetFiles(parent.FullName, "*.csproj");
            if (projects.Length != 0)
            {
                return projects;
            }
        }

        return [];
    }

    private static async Task<IReadOnlyList<RazorDiagnostic>> CheckProjectsAsync(string[] projects, string path, RazorStyleOptions options)
    {
        try
        {
            return await AnalyzeProjectsAsync(projects, path, options).ConfigureAwait(false);
        }
        catch (InvalidProjectFileException exception)
        {
            throw new InvalidOperationException("Cannot load project: " + exception.Message, exception);
        }
    }

    private static async Task<IReadOnlyList<RazorDiagnostic>> AnalyzeProjectsAsync(string[] projects, string path, RazorStyleOptions options)
    {
        List<RazorDiagnostic> diagnostics = [];
        HashSet<string> seen = [with(StringComparer.Ordinal)];
        foreach (string projectPath in projects)
        {
            Dictionary<string, string> properties = new()
            {
                ["RazorStyleEnabled"] = "false"
            };
            using ProjectCollection collection = new(properties);
            Microsoft.Build.Evaluation.Project evaluated = collection.LoadProject(projectPath);
            string[] frameworks = evaluated.GetPropertyValue("TargetFrameworks").Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (frameworks.Length == 0)
            {
                frameworks = [evaluated.GetPropertyValue("TargetFramework")];
            }
            foreach (string framework in frameworks)
            {
                properties["TargetFramework"] = framework;
                collection.SetGlobalProperty("TargetFramework", framework);
                evaluated.ReevaluateIfNecessary();
                string[] ruleIds = [ComponentMemberAnalyzer.OrderingId, ComponentMemberAnalyzer.NestedTypeId];
                HashSet<string> disabledRules = [.. ruleIds.Where(id => !options.IsRuleEnabled(id)
                    || evaluated.GetPropertyValue("Disable" + id).Equals("true", StringComparison.OrdinalIgnoreCase))];
                if (disabledRules.Count == ruleIds.Length)
                {
                    continue;
                }
                using MSBuildWorkspace workspace = MSBuildWorkspace.Create(properties);
                Microsoft.CodeAnalysis.Project project = await workspace.OpenProjectAsync(projectPath).ConfigureAwait(false);
                WorkspaceDiagnostic? failure = workspace.Diagnostics.FirstOrDefault(d => d.Kind == WorkspaceDiagnosticKind.Failure);
                if (failure is not null)
                {
                    throw new InvalidOperationException(failure.Message + " Restore the project before checking it.");
                }
                Compilation? compilation = await project.GetCompilationAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Cannot compile project: " + projectPath);
                Diagnostic? error = compilation.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
                if (error is not null)
                {
                    throw new InvalidOperationException("Cannot check component rules until the project compiles: " + error);
                }
                ImmutableDictionary<string, ReportDiagnostic> disabled = compilation.Options.SpecificDiagnosticOptions;
                foreach (string id in disabledRules)
                {
                    disabled = disabled.SetItem(id, ReportDiagnostic.Suppress);
                }

                compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(disabled));
                CompilationWithAnalyzers analysis = compilation.WithAnalyzers([new ComponentMemberAnalyzer()], project.AnalyzerOptions);
                foreach (Diagnostic diagnostic in await analysis.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false))
                {
                    if (diagnostic.Id == "AD0001")
                    {
                        throw new InvalidOperationException(diagnostic.ToString());
                    }
                    FileLinePositionSpan location = diagnostic.Location.GetLineSpan();
                    if (disabledRules.Contains(diagnostic.Id) || !IsInScope(location.Path, path)
                        || !seen.Add(diagnostic.Id + diagnostic.Location + diagnostic.GetMessage()))
                    {
                        continue;
                    }
                    diagnostics.Add(new RazorDiagnostic(diagnostic.Id, diagnostic.GetMessage(), location.Path,
                        location.StartLinePosition.Line + 1, location.StartLinePosition.Character + 1));
                }
            }
        }

        return diagnostics;
    }

    private static bool IsInScope(string file, string path)
    {
        return Directory.Exists(path)
            ? Path.GetRelativePath(path, file) is string relative && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative)
            : Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase) || file.Equals(path, StringComparison.OrdinalIgnoreCase) || (path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) && file.Equals(path + ".cs", StringComparison.OrdinalIgnoreCase));
    }
}
