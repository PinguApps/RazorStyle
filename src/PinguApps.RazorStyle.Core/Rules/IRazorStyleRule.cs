namespace PinguApps.RazorStyle.Core.Rules;

/// <summary>
/// Defines a Razor source style rule.
/// </summary>
public interface IRazorStyleRule
{
    /// <summary>
    /// Gets the rule identifier.
    /// </summary>
    public string DiagnosticId
    {
        get;
    }

    /// <summary>
    /// Analyzes and optionally fixes a Razor document.
    /// </summary>
    public RazorStyleRuleResult Evaluate(RazorStyleDocument document, bool applyFixes, string newLine);
}
