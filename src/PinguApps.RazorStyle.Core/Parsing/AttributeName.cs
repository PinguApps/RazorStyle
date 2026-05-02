namespace PinguApps.RazorStyle.Core.Parsing;

/// <summary>
/// Provides helpers for Razor attribute names.
/// </summary>
public static class AttributeName
{
    /// <summary>
    /// Gets the attribute name from raw attribute text.
    /// </summary>
    public static string GetName(string rawText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawText);

        int equalsIndex = rawText.IndexOf('=', StringComparison.Ordinal);
        string name = equalsIndex < 0 ? rawText : rawText[..equalsIndex];
        return name.Trim();
    }

    /// <summary>
    /// Gets the attribute value from raw attribute text, when one exists.
    /// </summary>
    public static string? GetValue(string rawText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawText);

        int equalsIndex = rawText.IndexOf('=', StringComparison.Ordinal);
        return equalsIndex < 0 ? null : rawText[(equalsIndex + 1)..].Trim();
    }
}

