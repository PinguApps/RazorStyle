namespace PinguApps.RazorStyle.Core.Parsing;

/// <summary>
/// Converts character indexes to one-based line and column positions.
/// </summary>
public sealed class LineMap
{
    private readonly int[] _lineStarts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LineMap"/> class.
    /// </summary>
    public LineMap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<int> lineStarts = [0];

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            lineStarts.Add(index + 1);
        }

        _lineStarts = [.. lineStarts];
    }

    /// <summary>
    /// Gets the one-based line and column for a zero-based character index.
    /// </summary>
    public LineColumn GetLineColumn(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must not be negative.");
        }

        int lineIndex = Array.BinarySearch(_lineStarts, index);
        if (lineIndex < 0)
        {
            lineIndex = ~lineIndex - 1;
        }

        return new LineColumn(lineIndex + 1, index - _lineStarts[lineIndex] + 1);
    }
}

