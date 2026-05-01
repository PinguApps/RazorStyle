namespace PinguApps.RazorStyle.Core.Running;

/// <summary>
/// Represents diagnostics and fixes produced by one Razor style rule.
/// </summary>
public sealed record RazorStyleRuleResult(
    IReadOnlyList<RazorDiagnostic> Diagnostics,
    IReadOnlyList<RazorStyleReplacement> Replacements);

