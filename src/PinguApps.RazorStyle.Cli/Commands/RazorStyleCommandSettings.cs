using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace PinguApps.RazorStyle.Cli.Commands;

/// <summary>
/// Represents shared RazorStyle command settings.
/// </summary>
public sealed class RazorStyleCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets the file or directory path to process.
    /// </summary>
    [CommandArgument(0, "<path>")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets rule IDs to disable.
    /// </summary>
    [CommandOption("--disable <RULE_ID>")]
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Spectre.Console.Cli binds repeatable option values to arrays.")]
    public string[] DisabledRules { get; init; } = [];
}

