using Spectre.Console.Cli;

namespace PinguApps.RazorStyle.Cli.Commands;

/// <summary>
/// Checks Razor markup and component code for RazorStyle rule violations.
/// </summary>
public sealed class CheckCommand : Command<RazorStyleCommandSettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, RazorStyleCommandSettings settings, CancellationToken cancellationToken)
    {
        return RazorStyleCliRunner.Run("check", settings);
    }
}
