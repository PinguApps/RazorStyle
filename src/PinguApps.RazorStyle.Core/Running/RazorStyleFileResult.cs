namespace PinguApps.RazorStyle.Core.Running;

/// <summary>
/// Represents the result of processing one Razor file.
/// </summary>
public sealed record RazorStyleFileResult(
    string FilePath,
    IReadOnlyList<RazorDiagnostic> Diagnostics,
    string? RewrittenText);

