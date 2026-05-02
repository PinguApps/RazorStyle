namespace PinguApps.RazorStyle.Core.Documents;

/// <summary>
/// Represents parsed state for one Razor file.
/// </summary>
public sealed class RazorStyleDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RazorStyleDocument"/> class.
    /// </summary>
    public RazorStyleDocument(string text, string filePath)
    {
        Text = text;
        FilePath = filePath;
        LineMap = new LineMap(text);
        Tags = new RazorTagScanner().Scan(text);
    }

    /// <summary>
    /// Gets the file text.
    /// </summary>
    public string Text
    {
        get;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath
    {
        get;
    }

    /// <summary>
    /// Gets the line map.
    /// </summary>
    public LineMap LineMap
    {
        get;
    }

    /// <summary>
    /// Gets parsed start tags.
    /// </summary>
    public IReadOnlyList<TagInfo> Tags
    {
        get;
    }
}

