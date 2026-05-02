using System.Text;

namespace PinguApps.RazorStyle.Core.Rules;

/// <summary>
/// Enforces the preferred attribute order for Razor start tags.
/// </summary>
public sealed class AttributeOrderRule : IRazorStyleRule
{
    /// <summary>
    /// The rule identifier.
    /// </summary>
    public const string DiagnosticId = "RS0003";

    /// <inheritdoc />
    string IRazorStyleRule.DiagnosticId => DiagnosticId;

    /// <inheritdoc />
    public RazorStyleRuleResult Evaluate(RazorStyleDocument document, bool applyFixes, string newLine)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(newLine);

        List<RazorDiagnostic> diagnostics = [];
        List<RazorStyleReplacement> replacements = [];

        foreach (TagInfo tag in document.Tags.Where(tag => tag.Attributes.Count > 1))
        {
            IReadOnlyList<AttributeInfo> orderedAttributes = OrderAttributes(tag.Attributes);
            if (AttributesMatch(tag.Attributes, orderedAttributes))
            {
                continue;
            }

            AttributeInfo firstOutOfOrderAttribute = FindFirstOutOfOrderAttribute(tag.Attributes, orderedAttributes);
            diagnostics.Add(new RazorDiagnostic(
                DiagnosticId,
                "Attributes must appear in the preferred RazorStyle order.",
                document.FilePath,
                firstOutOfOrderAttribute.Line,
                firstOutOfOrderAttribute.Column));

            if (!applyFixes)
            {
                continue;
            }

            string replacementText = FormatWithExistingLayout(document.Text, tag, orderedAttributes);
            string currentText = document.Text[tag.StartIndex..(tag.EndIndex + 1)];

            if (!string.Equals(currentText, replacementText, StringComparison.Ordinal))
            {
                replacements.Add(new RazorStyleReplacement(tag.StartIndex, tag.EndIndex, replacementText));
            }
        }

        return new RazorStyleRuleResult(diagnostics, replacements);
    }

    private static string FormatWithExistingLayout(string text, TagInfo tag, IReadOnlyList<AttributeInfo> orderedAttributes)
    {
        string tagText = text[tag.StartIndex..(tag.EndIndex + 1)];
        StringBuilder builder = new(tagText);

        for (int index = tag.Attributes.Count - 1; index >= 0; index--)
        {
            AttributeInfo targetSlot = tag.Attributes[index];
            AttributeInfo orderedAttribute = orderedAttributes[index];
            int slotStart = targetSlot.StartIndex - tag.StartIndex;
            int slotLength = targetSlot.EndIndex - targetSlot.StartIndex + 1;

            builder.Remove(slotStart, slotLength);
            builder.Insert(slotStart, orderedAttribute.RawText);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<AttributeInfo> OrderAttributes(IReadOnlyList<AttributeInfo> attributes)
    {
        return attributes
            .Select((attribute, index) => (Attribute: attribute, Index: index, Rank: GetRank(attribute.RawText)))
            .OrderBy(attribute => attribute.Rank)
            .ThenBy(attribute => attribute.Index)
            .Select(attribute => attribute.Attribute)
            .ToArray();
    }

    private static bool AttributesMatch(IReadOnlyList<AttributeInfo> current, IReadOnlyList<AttributeInfo> expected)
    {
        for (int index = 0; index < current.Count; index++)
        {
            if (!string.Equals(current[index].RawText, expected[index].RawText, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static AttributeInfo FindFirstOutOfOrderAttribute(IReadOnlyList<AttributeInfo> current, IReadOnlyList<AttributeInfo> expected)
    {
        for (int index = 0; index < current.Count; index++)
        {
            if (!string.Equals(current[index].RawText, expected[index].RawText, StringComparison.Ordinal))
            {
                return current[index];
            }
        }

        return current[0];
    }

    private static int GetRank(string rawText)
    {
        string name = AttributeName.GetName(rawText);
        string? value = AttributeName.GetValue(rawText);

        return IsBooleanAttribute(value)
            ? 35
            : name switch
            {
                "@key" => 0,
                "@ref" => 1,
                "name" or "Name" => 2,
                "id" or "Id" => 3,
                "class" or "Class" => 4,
                "style" or "Style" => 5,
                "@bind" => 6,
                "ValueExpression" => 8,
                "@onclick" or "@OnClick" => 9,
                "@onchange" or "@OnChange" => 10,
                "@oninput" or "@OnInput" => 11,
                "@onfocus" or "@OnFocus" => 12,
                "@onblur" or "@OnBlur" => 13,
                "@onkeydown" or "@OnKeyDown" => 14,
                "@onkeyup" or "@OnKeyUp" => 15,
                "ValueChanged" => 17,
                "SelectedValueChanged" => 18,
                "OnSubmit" => 19,
                "OnValidSubmit" => 20,
                "OnInvalidSubmit" => 21,
                "type" => 22,
                "title" or "Title" => 23,
                "value" or "Value" => 24,
                "src" or "Src" => 25,
                "href" or "Href" => 26,
                "EditContext" => 27,
                "for" or "For" => 28,
                "ValidationFor" => 29,
                "ChildContent" => 30,
                "@attributes" => 34,
                _ when name.StartsWith("@bind-", StringComparison.Ordinal) => 7,
                _ when name.StartsWith('@') && name.Contains(':', StringComparison.Ordinal) => 16,
                _ when name.EndsWith("Template", StringComparison.Ordinal) => 30,
                _ when name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase) => 32,
                _ when name.StartsWith("data-", StringComparison.OrdinalIgnoreCase) => 33,
                _ => 31,
            };
    }

    private static bool IsBooleanAttribute(string? value)
    {
        if (value is null)
        {
            return true;
        }

        string unquotedValue = value.Trim().Trim('"', '\'');
        return string.Equals(unquotedValue, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unquotedValue, "false", StringComparison.OrdinalIgnoreCase) ||
            unquotedValue.Contains(" > ", StringComparison.Ordinal) ||
            unquotedValue.Contains(" < ", StringComparison.Ordinal) ||
            unquotedValue.Contains(" == ", StringComparison.Ordinal) ||
            unquotedValue.Contains(" != ", StringComparison.Ordinal) ||
            unquotedValue.Contains(" >= ", StringComparison.Ordinal) ||
            unquotedValue.Contains(" <= ", StringComparison.Ordinal);
    }

}

