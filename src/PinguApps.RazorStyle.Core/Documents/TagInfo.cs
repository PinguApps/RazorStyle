namespace PinguApps.RazorStyle.Core.Documents;

/// <summary>
/// Represents a parsed Razor start tag.
/// </summary>
public sealed record TagInfo(
    string Name,
    int StartIndex,
    int EndIndex,
    int NameLine,
    int NameColumn,
    bool IsSelfClosing,
    IReadOnlyList<AttributeInfo> Attributes);

