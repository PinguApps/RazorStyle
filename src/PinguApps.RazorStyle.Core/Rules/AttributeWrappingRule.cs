namespace PinguApps.RazorStyle.Core.Rules;

/// <summary>
/// Enforces Razor start-tag attribute wrapping and alignment.
/// </summary>
public sealed class AttributeWrappingRule : IRazorStyleRule
{
    /// <summary>
    /// The rule identifier.
    /// </summary>
    public const string DiagnosticId = "RS0001";

    private readonly AttributeWrappingFixer _fixer = new();

    /// <inheritdoc />
    string IRazorStyleRule.DiagnosticId => DiagnosticId;

    /// <inheritdoc />
    public RazorStyleRuleResult Evaluate(RazorStyleDocument document, bool applyFixes, string newLine)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(newLine);

        List<RazorDiagnostic> diagnostics = [];
        List<RazorStyleReplacement> replacements = [];

        foreach (TagInfo tag in document.Tags)
        {
            IReadOnlyList<RazorDiagnostic> tagDiagnostics = Analyze(tag, document.FilePath);
            diagnostics.AddRange(tagDiagnostics);

            if (!applyFixes || tagDiagnostics.Count == 0 || tag.Attributes.Count == 0)
            {
                continue;
            }

            string replacementText = _fixer.Format(tag, newLine);
            string currentText = document.Text[tag.StartIndex..(tag.EndIndex + 1)];

            if (!string.Equals(currentText, replacementText, StringComparison.Ordinal))
            {
                replacements.Add(new RazorStyleReplacement(tag.StartIndex, tag.EndIndex, replacementText));
            }
        }

        return new RazorStyleRuleResult(diagnostics, replacements);
    }

    /// <summary>
    /// Analyzes one tag and returns any diagnostics.
    /// </summary>
    public IReadOnlyList<RazorDiagnostic> Analyze(TagInfo tag, string filePath)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(filePath);

        if (tag.Attributes.Count == 0)
        {
            return [];
        }

        if (tag.Attributes.Count == 1)
        {
            AttributeInfo attribute = tag.Attributes[0];
            return attribute.Line == tag.NameLine
                ? []
                : [CreateDiagnostic(filePath, attribute, "Single attribute must appear on the same line as the tag name.")];
        }

        List<RazorDiagnostic> diagnostics = [];
        AttributeInfo firstAttribute = tag.Attributes[0];

        if (firstAttribute.Line != tag.NameLine)
        {
            diagnostics.Add(CreateDiagnostic(filePath, firstAttribute, "First attribute must appear on the same line as the tag name."));
        }

        int expectedColumn = firstAttribute.Column;
        for (int index = 1; index < tag.Attributes.Count; index++)
        {
            AttributeInfo attribute = tag.Attributes[index];
            AttributeInfo previousAttribute = tag.Attributes[index - 1];

            if (attribute.Line == previousAttribute.Line)
            {
                diagnostics.Add(CreateDiagnostic(filePath, attribute, "Each attribute after the first must begin on a new line."));
                continue;
            }

            if (attribute.Column != expectedColumn)
            {
                diagnostics.Add(CreateDiagnostic(filePath, attribute, "Wrapped attributes must align with the first attribute."));
            }
        }

        return diagnostics;
    }

    private static RazorDiagnostic CreateDiagnostic(string filePath, AttributeInfo attribute, string message)
    {
        return new RazorDiagnostic(DiagnosticId, message, filePath, attribute.Line, attribute.Column);
    }
}

