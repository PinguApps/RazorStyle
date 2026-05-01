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

        RazorStyleOptions options = new(settings.DisabledRules);
        RazorStyleRunner runner = new();

        try
        {
            IReadOnlyList<string> files = RazorStyleRunner.FindRazorFiles(settings.Path);
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

            return command == "fix"
                ? WriteFixSummary(changedFileCount, diagnosticCount)
                : WriteCheckSummary(files.Count, diagnosticCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
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

        AnsiConsole.MarkupLine(CultureInfo.InvariantCulture, "[yellow]{0}[/]", Markup.Escape(message));
    }
}

