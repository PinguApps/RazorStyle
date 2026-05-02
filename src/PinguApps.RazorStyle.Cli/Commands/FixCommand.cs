using Spectre.Console.Cli;

namespace PinguApps.RazorStyle.Cli.Commands;

/// <summary>
/// Fixes RazorStyle rule violations in Razor files.
/// </summary>
public sealed class FixCommand : Command<RazorStyleCommandSettings>
{
    /// <inheritdoc />
    protected override int Execute(CommandContext context, RazorStyleCommandSettings settings, CancellationToken cancellationToken)
    {
        return RazorStyleCliRunner.Run("fix", settings);
    }
}

