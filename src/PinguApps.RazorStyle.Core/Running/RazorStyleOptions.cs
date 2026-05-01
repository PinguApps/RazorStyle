namespace PinguApps.RazorStyle.Core.Running;

/// <summary>
/// Represents RazorStyle processing options.
/// </summary>
public sealed class RazorStyleOptions
{
    /// <summary>
    /// Gets a default options instance.
    /// </summary>
    public static RazorStyleOptions Default { get; } = new([]);

    /// <summary>
    /// Initializes a new instance of the <see cref="RazorStyleOptions"/> class.
    /// </summary>
    public RazorStyleOptions(IEnumerable<string> disabledRuleIds)
    {
        ArgumentNullException.ThrowIfNull(disabledRuleIds);

        DisabledRuleIds = disabledRuleIds
            .Where(ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets disabled rule IDs.
    /// </summary>
    public IReadOnlySet<string> DisabledRuleIds
    {
        get;
    }

    /// <summary>
    /// Returns true when the rule is enabled.
    /// </summary>
    public bool IsRuleEnabled(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        return !DisabledRuleIds.Contains(ruleId);
    }
}

