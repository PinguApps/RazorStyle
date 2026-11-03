using System.Globalization;
using System.Text;
using Spectre.Console;

namespace PinguApps.RazorStyle.Cli.Infrastructure;

/// <summary>
/// Runs RazorStyle CLI commands.
/// </summary>
public static class RazorStyleCliRunner
{
    /// <summary>
    /// Runs a RazorStyle command.
    /// </summary>
    public static int Run(string command, RazorStyleCommandSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(settings);

        RazorStyleOptions options = new(settings.DisabledRules)
        {
            PreserveAttributeSplattingOrder = settings.PreserveAttributeSplattingOrder,
        };
        RazorStyleRunner runner = new();

        try
        {
            string markupPath = settings.Path;
            if (File.Exists(settings.Path) && Path.GetExtension(settings.Path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                markupPath = Path.GetDirectoryName(Path.GetFullPath(settings.Path))!;
            }
            else if (settings.Path.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase) && File.Exists(settings.Path[..^3]))
            {
                markupPath = settings.Path[..^3];
            }
            IReadOnlyList<string> files = RazorStyleRunner.FindRazorFiles(markupPath);
            int diagnosticCount = 0;
            int changedFileCount = 0;

            foreach (string file in files)
            {
                string text = RazorStyleRunner.ReadAllTextPreservingEncoding(file, out Encoding encoding);
                RazorStyleFileResult result = command == "check"
                    ? runner.CheckText(text, file, options)
                    : runner.FixText(text, file, options);

                foreach (RazorDiagnostic diagnostic in result.Diagnostics)
                {
                    diagnosticCount++;
                    if (command == "check")
                    {
                        WriteDiagnostic(diagnostic);
                    }
                }

                if (command == "fix" && result.RewrittenText is not null)
                {
                    RazorStyleRunner.WriteAllText(file, result.RewrittenText, encoding);
                    changedFileCount++;
                }
            }

            if (command == "check" && (options.IsRuleEnabled("PARS0004") || options.IsRuleEnabled("PARS0005")))
            {
                IReadOnlyList<RazorDiagnostic> componentDiagnostics = ComponentProjectChecker.CheckAsync(settings.Path, options).GetAwaiter().GetResult();
                foreach (RazorDiagnostic diagnostic in componentDiagnostics)
                {
                    diagnosticCount++;
                    WriteDiagnostic(diagnostic);
                }
            }

            return command == "fix"
                ? WriteFixSummary(changedFileCount, diagnosticCount)
                : WriteCheckSummary(files.Count, diagnosticCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine(CultureInfo.InvariantCulture, "[red]RazorStyle error:[/] {0}", exception.Message);
            return 2;
        }
    }

    private static int WriteFixSummary(int changedFileCount, int diagnosticCount)
    {
        AnsiConsole.MarkupLine(
            CultureInfo.InvariantCulture,
            "[green]RazorStyle fixed {0} file(s); {1} violation(s) found before fixing.[/]",
            changedFileCount,
            diagnosticCount);

        return 0;
    }

    private static int WriteCheckSummary(int fileCount, int diagnosticCount)
    {
        if (diagnosticCount == 0)
        {
            AnsiConsole.MarkupLine(
                CultureInfo.InvariantCulture,
                "[green]RazorStyle checked {0} file(s); no violations found.[/]",
                fileCount);

            return 0;
        }

        AnsiConsole.MarkupLine(
            CultureInfo.InvariantCulture,
            "[red]RazorStyle checked {0} file(s); {1} violation(s) found.[/]",
            fileCount,
            diagnosticCount);

        return 1;
    }

    private static void WriteDiagnostic(RazorDiagnostic diagnostic)
    {
        string path = Path.GetRelativePath(Environment.CurrentDirectory, diagnostic.FilePath);
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"{path}({diagnostic.Line},{diagnostic.Column}): warning {diagnostic.Id}: {diagnostic.Message}");

        Console.Out.WriteLine(message);
    }
}

