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

            string indent = GetEffectiveLineIndent(document, tag);
            string childIndent = indent + "    ";

            if (contentStartsOnStartTagLine)
            {
                replacements.Add(new RazorStyleReplacement(tag.EndIndex + 1, firstContentIndex - 1, newLine + childIndent));
                AddContentIndentReplacements(document.Text, tag.EndIndex + 1, closingTag.StartIndex, childIndent, replacements);
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
            if (text[index] == '@' && TryReadRazorBlockOrExpressionEnd(text, index, out int expressionEnd))
            {
                index = expressionEnd;
                continue;
            }

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

            if (TryReadStartTag(text, index, out string? childTagName, out int childTagEnd, out _) &&
                childTagName is not null &&
                IsRawTextTag(childTagName))
            {
                int rawTextClosingTagEnd = FindRawTextClosingTagEnd(text, childTagName, childTagEnd);
                if (rawTextClosingTagEnd < 0)
                {
                    return null;
                }

                index = rawTextClosingTagEnd;
                continue;
            }

            if (IsSameNameClosingTag(text, index, tag.Name))
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
        return TryReadStartTag(text, index, out string? actualTagName, out tagEnd, out isSelfClosing) &&
            string.Equals(actualTagName, tagName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadStartTag(
        string text,
        int index,
        out string? tagName,
        out int tagEnd,
        out bool isSelfClosing)
    {
        tagName = null;
        tagEnd = -1;
        isSelfClosing = false;

        if (index + 1 >= text.Length || text[index] != '<' || !IsTagNameStart(text[index + 1]))
        {
            return false;
        }

        int nameStart = index + 1;
        int cursor = nameStart + 1;
        while (cursor < text.Length && IsTagNamePart(text[cursor]))
        {
            cursor++;
        }

        tagName = text[nameStart..cursor];
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

    private static int FindRawTextClosingTagEnd(string text, string tagName, int startIndex)
    {
        int index = startIndex;

        while (index < text.Length)
        {
            int closingTagIndex = text.IndexOf("</" + tagName, index, StringComparison.OrdinalIgnoreCase);
            if (closingTagIndex < 0)
            {
                return -1;
            }

            if (IsSameNameClosingTag(text, closingTagIndex, tagName))
            {
                return text.IndexOf('>', closingTagIndex + tagName.Length + 2);
            }

            index = closingTagIndex + 2;
        }

        return -1;
    }

    private static bool TryReadRazorBlockOrExpressionEnd(string text, int index, out int expressionEnd)
    {
        expressionEnd = -1;

        if (index + 1 >= text.Length || text[index] != '@')
        {
            return false;
        }

        if (text[index + 1] is '(' or '{')
        {
            char open = text[index + 1];
            char close = open == '(' ? ')' : '}';
            return TryReadBalancedRegionEnd(text, index + 1, open, close, out expressionEnd);
        }

        if (!IsTagNameStart(text[index + 1]))
        {
            return false;
        }

        int cursor = index + 2;
        while (cursor < text.Length && (IsTagNamePart(text[cursor]) || text[cursor] == '.'))
        {
            cursor++;
        }

        return cursor < text.Length &&
            text[cursor] == '(' &&
            TryReadBalancedRegionEnd(text, cursor, '(', ')', out expressionEnd);
    }

    private static bool TryReadBalancedRegionEnd(
        string text,
        int startIndex,
        char open,
        char close,
        out int expressionEnd)
    {
        expressionEnd = -1;
        int depth = 0;
        char? quote = null;
        bool escaped = false;

        for (int cursor = startIndex; cursor < text.Length; cursor++)
        {
            char current = text[cursor];

            if (quote is not null)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

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

            if (current == open)
            {
                depth++;
                continue;
            }

            if (current == close)
            {
                depth--;
                if (depth == 0)
                {
                    expressionEnd = cursor;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSameNameClosingTag(string text, int index, string tagName)
    {
        if (!StartsWith(text, index, "</" + tagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int cursor = index + tagName.Length + 2;
        return cursor >= text.Length || !IsTagNamePart(text[cursor]);
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

    private static void AddContentIndentReplacements(
        string text,
        int startIndex,
        int endIndex,
        string indent,
        List<RazorStyleReplacement> replacements)
    {
        for (int index = startIndex; index < endIndex; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            int insertionIndex = index + 1;
            if (FindFirstNonWhitespaceIndex(text, insertionIndex, endIndex) >= endIndex)
            {
                continue;
            }

            replacements.Add(new RazorStyleReplacement(insertionIndex, insertionIndex - 1, indent));
        }
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

    private static string GetEffectiveLineIndent(RazorStyleDocument document, TagInfo tag)
    {
        string indent = GetLineIndent(document.Text, tag.StartIndex);
        LineColumn tagPosition = document.LineMap.GetLineColumn(tag.StartIndex);

        foreach (TagInfo parent in document.Tags.Where(parent => !parent.IsSelfClosing && parent.StartIndex < tag.StartIndex))
        {
            ClosingTagInfo? closingTag = FindMatchingClosingTag(document.Text, parent);
            if (closingTag is null || closingTag.StartIndex <= tag.StartIndex)
            {
                continue;
            }

            LineColumn parentEndPosition = document.LineMap.GetLineColumn(parent.EndIndex);
            if (parentEndPosition.Line == tagPosition.Line)
            {
                indent += "    ";
            }
        }

        return indent;
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

    private static bool IsTagNameStart(char value)
    {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsRawTextTag(string tagName)
    {
        return string.Equals(tagName, "script", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase);
    }

}

