using Reqnroll;
using Xunit;

namespace PinguApps.RazorStyle.Tests.Steps;

[Binding]
public sealed class RazorStyleRulesSteps
{
    private readonly RazorStyleRunner _runner = new();
    private readonly List<string> _disabledRuleIds = [];
    private string? _source;
    private IReadOnlyList<TagInfo> _tags = [];
    private RazorStyleFileResult? _result;

    [Given("the Razor source is")]
    public void GivenTheRazorSourceIs(string source)
    {
        _source = source;
        _disabledRuleIds.Clear();
    }

    [Given("RazorStyle rule {string} is disabled")]
    public void GivenRazorStyleRuleIsDisabled(string ruleId)
    {
        _disabledRuleIds.Add(ruleId);
    }

    [When("the Razor tags are scanned")]
    public void WhenTheRazorTagsAreScanned()
    {
        Assert.NotNull(_source);

        _tags = new RazorTagScanner().Scan(_source);
    }

    [When("RazorStyle check runs")]
    public void WhenRazorStyleCheckRuns()
    {
        Assert.NotNull(_source);

        _result = _runner.CheckText(_source, "Test.razor", new RazorStyleOptions(_disabledRuleIds));
    }

    [When("RazorStyle fix runs")]
    public void WhenRazorStyleFixRuns()
    {
        Assert.NotNull(_source);

        _result = _runner.FixText(_source, "Test.razor", new RazorStyleOptions(_disabledRuleIds));
    }

    [Then("{int} Razor start tags should be found")]
    public void ThenRazorStartTagsShouldBeFound(int count)
    {
        Assert.Equal(count, _tags.Count);
    }

    [Then("tag {int} should have attributes {string}")]
    public void ThenTagShouldHaveAttributes(int tagNumber, string expectedAttributes)
    {
        TagInfo tag = _tags[tagNumber - 1];
        string actualAttributes = string.Join(",", tag.Attributes.Select(attribute => attribute.RawText));

        Assert.Equal(expectedAttributes.Replace("\\\"", "\"", StringComparison.Ordinal), actualAttributes);
    }

    [Then("tag {int} should be named {string}")]
    public void ThenTagShouldBeNamed(int tagNumber, string expectedName)
    {
        TagInfo tag = _tags[tagNumber - 1];

        Assert.Equal(expectedName, tag.Name);
    }

    [Then("tag {int} should have these attributes")]
    public void ThenTagShouldHaveTheseAttributes(int tagNumber, Table expectedAttributes)
    {
        Assert.Equal(
            expectedAttributes.Rows.Select(row => row["Attribute"]),
            _tags[tagNumber - 1].Attributes.Select(attribute => attribute.RawText));
    }

    [Then("the rewritten source should pass check and remain unchanged by another fix")]
    public void ThenTheRewrittenSourceShouldPassCheckAndRemainUnchangedByAnotherFix()
    {
        Assert.NotNull(_result?.RewrittenText);

        RazorStyleOptions options = new(_disabledRuleIds);
        Assert.Empty(_runner.CheckText(_result.RewrittenText, "Test.razor", options).Diagnostics);
        Assert.Null(_runner.FixText(_result.RewrittenText, "Test.razor", options).RewrittenText);
    }

    [Then("no RazorStyle diagnostics should be reported")]
    public void ThenNoRazorStyleDiagnosticsShouldBeReported()
    {
        Assert.NotNull(_result);
        Assert.Empty(_result.Diagnostics);
    }

    [Then("RazorStyle should report {string}")]
    public void ThenRazorStyleShouldReport(string expected)
    {
        Assert.NotNull(_result);

        Assert.Contains(
            _result.Diagnostics,
            diagnostic => string.Equals(diagnostic.Id, expected, StringComparison.Ordinal) ||
                string.Equals(diagnostic.Message, expected, StringComparison.Ordinal));
    }

    [Then("the rewritten Razor source should be")]
    public void ThenTheRewrittenRazorSourceShouldBe(string expected)
    {
        Assert.NotNull(_result);

        Assert.Equal(expected, _result.RewrittenText);
    }

    [Then("the Razor source should not be rewritten")]
    public void ThenTheRazorSourceShouldNotBeRewritten()
    {
        Assert.NotNull(_result);

        Assert.Null(_result.RewrittenText);
    }
}
