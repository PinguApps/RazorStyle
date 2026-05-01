namespace PinguApps.RazorStyle.Core.Rules;

/// <summary>
/// Enforces child content on its own line between start and end tags.
/// </summary>
public sealed class ChildContentLineRule : IRazorStyleRule
{
    /// <summary>
    /// The rule identifier.
    /// </summary>
    public const string DiagnosticId = "RS0002";

    /// <inheritdoc />
    string IRazorStyleRule.DiagnosticId => DiagnosticId;

    /// <inheritdoc />
    public RazorStyleRuleResult Evaluate(RazorStyleDocument document, bool applyFixes, string newLine)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(newLine);

        List<RazorDiagnostic> diagnostics = [];
        List<RazorStyleReplacement> replacements = [];

        foreach (TagInfo tag in document.Tags.Where(tag => !tag.IsSelfClosing))
        {
            ClosingTagInfo? closingTag = FindMatchingClosingTag(document.Text, tag);
            if (closingTag is null)
            {
                continue;
            }

            string content = document.Text[(tag.EndIndex + 1)..closingTag.StartIndex];
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            int firstContentIndex = FindFirstNonWhitespaceIndex(document.Text, tag.EndIndex + 1, closingTag.StartIndex);
            int lastContentIndex = FindLastNonWhitespaceIndex(document.Text, tag.EndIndex + 1, closingTag.StartIndex);
            LineColumn firstContentPosition = document.LineMap.GetLineColumn(firstContentIndex);
            LineColumn lastContentPosition = document.LineMap.GetLineColumn(lastContentIndex);
            LineColumn startTagEndPosition = document.LineMap.GetLineColumn(tag.EndIndex);
            LineColumn closingTagPosition = document.LineMap.GetLineColumn(closingTag.StartIndex);

            bool contentStartsOnStartTagLine = firstContentPosition.Line == startTagEndPosition.Line;
            bool closingTagSharesContentLine = lastContentPosition.Line == closingTagPosition.Line;

            if (!contentStartsOnStartTagLine && !closingTagSharesContentLine)
            {
                continue;
            }

            diagnostics.Add(new RazorDiagnostic(
                DiagnosticId,
                "Child content must appear on its own line.",
                document.FilePath,
                firstContentPosition.Line,
                firstContentPosition.Column));

            if (!applyFixes)
            {
                continue;
            }

            string indent = GetLineIndent(document.Text, tag.StartIndex);
            string childIndent = indent + "    ";

            if (contentStartsOnStartTagLine)
            {
                replacements.Add(new RazorStyleReplacement(tag.EndIndex + 1, firstContentIndex - 1, newLine + childIndent));
            }

            if (closingTagSharesContentLine)
            {
                replacements.Add(new RazorStyleReplacement(lastContentIndex + 1, closingTag.StartIndex - 1, newLine + indent));
            }
        }

        return new RazorStyleRuleResult(diagnostics, replacements);
    }

    private static ClosingTagInfo? FindMatchingClosingTag(string text, TagInfo tag)
    {
        int depth = 0;

        for (int index = tag.EndIndex + 1; index < text.Length; index++)
        {
            if (text[index] != '<')
            {
                continue;
            }

            if (StartsWith(text, index, "<!--", StringComparison.Ordinal))
            {
                int commentEnd = text.IndexOf("-->", index + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    return null;
                }

                index = commentEnd + 2;
                continue;
            }

            if (StartsWith(text, index, "</" + tag.Name, StringComparison.OrdinalIgnoreCase))
            {
                int closeEnd = text.IndexOf('>', index + tag.Name.Length + 2);
                if (closeEnd < 0)
                {
                    return null;
                }

                if (depth == 0)
                {
                    return new ClosingTagInfo(index, closeEnd);
                }

                depth--;
                index = closeEnd;
                continue;
            }

            if (TryReadSameNameStartTag(text, index, tag.Name, out int startTagEnd, out bool isSelfClosing))
            {
                if (!isSelfClosing)
                {
                    depth++;
                }

                index = startTagEnd;
            }
        }

        return null;
    }

    private static bool TryReadSameNameStartTag(
        string text,
        int index,
        string tagName,
        out int tagEnd,
        out bool isSelfClosing)
    {
        tagEnd = -1;
        isSelfClosing = false;

        if (!StartsWith(text, index, "<" + tagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int cursor = index + tagName.Length + 1;
        if (cursor < text.Length && IsTagNamePart(text[cursor]))
        {
            return false;
        }

        char? quote = null;
        int parenthesisDepth = 0;

        for (; cursor < text.Length; cursor++)
        {
            char current = text[cursor];

            if (quote is not null)
            {
                if (current == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                parenthesisDepth++;
                continue;
            }

            if (current == ')' && parenthesisDepth > 0)
            {
                parenthesisDepth--;
                continue;
            }

            if (parenthesisDepth != 0 || current != '>')
            {
                continue;
            }

            tagEnd = cursor;
            int previous = cursor - 1;
            while (previous > index && char.IsWhiteSpace(text[previous]))
            {
                previous--;
            }

            isSelfClosing = text[previous] == '/';
            return true;
        }

        return false;
    }

    private static int FindFirstNonWhitespaceIndex(string text, int startIndex, int endIndex)
    {
        for (int index = startIndex; index < endIndex; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return startIndex;
    }

    private static int FindLastNonWhitespaceIndex(string text, int startIndex, int endIndex)
    {
        for (int index = endIndex - 1; index >= startIndex; index--)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return startIndex;
    }

    private static string GetLineIndent(string text, int index)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        int cursor = lineStart;

        while (cursor < text.Length && text[cursor] is ' ' or '\t')
        {
            cursor++;
        }

        return text[lineStart..cursor];
    }

    private static bool StartsWith(string text, int index, string value, StringComparison comparison)
    {
        return index + value.Length <= text.Length &&
            string.Compare(text, index, value, 0, value.Length, comparison) == 0;
    }

    private static bool IsTagNamePart(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or ':';
    }

}

