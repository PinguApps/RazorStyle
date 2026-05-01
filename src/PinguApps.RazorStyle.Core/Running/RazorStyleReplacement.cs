namespace PinguApps.RazorStyle.Core.Running;

/// <summary>
/// Represents a text replacement in a Razor file.
/// </summary>
public sealed record RazorStyleReplacement(int StartIndex, int EndIndex, string NewText);

