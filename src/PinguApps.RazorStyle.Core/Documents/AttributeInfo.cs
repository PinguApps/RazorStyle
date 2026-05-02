namespace PinguApps.RazorStyle.Core.Documents;

/// <summary>
/// Represents a parsed Razor start-tag attribute.
/// </summary>
public sealed record AttributeInfo(
    string RawText,
    int StartIndex,
    int EndIndex,
    int Line,
    int Column);

