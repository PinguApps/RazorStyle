using Spectre.Console.Cli;

namespace PinguApps.RazorStyle.Cli.Commands;

/// <summary>
/// Checks Razor files for RazorStyle rule violations.
/// </summary>
public sealed class CheckCommand : Command<RazorStyleCommandSettings>
{
    /// <inheritdoc />
    protected override int Execute(CommandContext context, RazorStyleCommandSettings settings, CancellationToken cancellationToken)
    {
        return RazorStyleCliRunner.Run("check", settings);
    }
}

