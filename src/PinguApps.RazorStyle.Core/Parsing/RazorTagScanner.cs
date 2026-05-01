namespace PinguApps.RazorStyle.Core.Parsing;

/// <summary>
/// Scans Razor source text for safely parsed start tags.
/// </summary>
public sealed class RazorTagScanner
{
    /// <summary>
    /// Scans the provided Razor text for start tags.
    /// </summary>
    public IReadOnlyList<TagInfo> Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        LineMap lineMap = new(text);
        List<TagInfo> tags = [];

        for (int index = 0; index < text.Length; index++)
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
                    break;
                }

                index = commentEnd + 2;
                continue;
            }

            if (index + 1 >= text.Length || text[index + 1] is '/' or '!' or '?' || !IsTagNameStart(text[index + 1]))
            {
                continue;
            }

            if (!TryParseTag(text, index, lineMap, out TagInfo? tag) || tag is null)
            {
                continue;
            }

            tags.Add(tag);

            if (IsRawTextTag(tag.Name))
            {
                int closingTagIndex = text.IndexOf(
                    "</" + tag.Name,
                    tag.EndIndex,
                    StringComparison.OrdinalIgnoreCase);

                index = closingTagIndex < 0 ? tag.EndIndex : closingTagIndex - 1;
            }
            else
            {
                index = tag.EndIndex;
            }
        }

        return tags;
    }

    private static bool TryParseTag(string text, int tagStart, LineMap lineMap, out TagInfo? tag)
    {
        tag = null;

        int nameStart = tagStart + 1;
        int cursor = nameStart + 1;

        while (cursor < text.Length && IsTagNamePart(text[cursor]))
        {
            cursor++;
        }

        string tagName = text[nameStart..cursor];
        List<AttributeInfo> attributes = [];
        bool isSelfClosing = false;

        while (cursor < text.Length)
        {
            cursor = SkipWhitespace(text, cursor);

            if (cursor >= text.Length)
            {
                return false;
            }

            if (text[cursor] == '>')
            {
                LineColumn namePosition = lineMap.GetLineColumn(nameStart);
                tag = new TagInfo(tagName, tagStart, cursor, namePosition.Line, namePosition.Column, isSelfClosing, attributes);
                return true;
            }

            if (text[cursor] == '/' && cursor + 1 < text.Length && text[cursor + 1] == '>')
            {
                isSelfClosing = true;
                LineColumn namePosition = lineMap.GetLineColumn(nameStart);
                tag = new TagInfo(tagName, tagStart, cursor + 1, namePosition.Line, namePosition.Column, isSelfClosing, attributes);
                return true;
            }

            if (text[cursor] == '<')
            {
                return false;
            }

            if (!TryReadAttribute(text, cursor, lineMap, out AttributeInfo? attribute) || attribute is null)
            {
                return false;
            }

            attributes.Add(attribute);
            cursor = attribute.EndIndex + 1;
        }

        return false;
    }

    private static bool TryReadAttribute(string text, int attributeStart, LineMap lineMap, out AttributeInfo? attribute)
    {
        attribute = null;

        int cursor = attributeStart;
        char? quote = null;
        int parenthesisDepth = 0;
        int lastNonWhitespace = attributeStart;

        while (cursor < text.Length)
        {
            char current = text[cursor];

            if (quote is not null)
            {
                if (current == quote)
                {
                    quote = null;
                }

                cursor++;
                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                lastNonWhitespace = cursor;
                cursor++;
                continue;
            }

            if (current == '(')
            {
                parenthesisDepth++;
                lastNonWhitespace = cursor;
                cursor++;
                continue;
            }

            if (current == ')' && parenthesisDepth > 0)
            {
                parenthesisDepth--;
                lastNonWhitespace = cursor;
                cursor++;
                continue;
            }

            if (parenthesisDepth == 0)
            {
                if (current == '>' || (current == '/' && cursor + 1 < text.Length && text[cursor + 1] == '>'))
                {
                    break;
                }

                if (current == '<')
                {
                    return false;
                }

                if (char.IsWhiteSpace(current))
                {
                    int nextNonWhitespace = SkipWhitespace(text, cursor);
                    if (nextNonWhitespace >= text.Length || text[nextNonWhitespace] == '>' ||
                        (text[nextNonWhitespace] == '/' && nextNonWhitespace + 1 < text.Length && text[nextNonWhitespace + 1] == '>'))
                    {
                        break;
                    }

                    if (text[nextNonWhitespace] != '=' && text[lastNonWhitespace] != '=')
                    {
                        break;
                    }
                }
            }

            if (!char.IsWhiteSpace(current))
            {
                lastNonWhitespace = cursor;
            }

            cursor++;
        }

        if (quote is not null || parenthesisDepth != 0 || cursor == attributeStart)
        {
            return false;
        }

        int attributeEnd = cursor - 1;
        while (attributeEnd >= attributeStart && char.IsWhiteSpace(text[attributeEnd]))
        {
            attributeEnd--;
        }

        if (attributeEnd < attributeStart)
        {
            return false;
        }

        LineColumn position = lineMap.GetLineColumn(attributeStart);
        attribute = new AttributeInfo(text[attributeStart..(attributeEnd + 1)], attributeStart, attributeEnd, position.Line, position.Column);
        return true;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsTagNameStart(char value)
    {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsTagNamePart(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or ':';
    }

    private static bool IsRawTextTag(string tagName)
    {
        return string.Equals(tagName, "script", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(string text, int index, string value, StringComparison comparison)
    {
        return index + value.Length <= text.Length &&
            string.Compare(text, index, value, 0, value.Length, comparison) == 0;
    }
}

