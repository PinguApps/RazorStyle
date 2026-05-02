namespace PinguApps.RazorStyle.Core.Diagnostics;

/// <summary>
/// Represents a Razor style diagnostic.
/// </summary>
public sealed record RazorDiagnostic(
    string Id,
    string Message,
    string FilePath,
    int Line,
    int Column);

