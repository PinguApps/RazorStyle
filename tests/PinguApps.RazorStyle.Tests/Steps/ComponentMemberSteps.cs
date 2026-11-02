using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using PinguApps.RazorStyle.CodeAnalysis;
using Reqnroll;
using Xunit;

namespace PinguApps.RazorStyle.Tests.Steps;

[Binding]
public sealed class ComponentMemberSteps : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private Document? _document;
    private ImmutableArray<Diagnostic> _diagnostics;
    private readonly List<CodeAction> _actions = [];

    [Given("the component C# source is")]
    public void GivenSource(string source)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Append(typeof(ComponentBase).Assembly.Location).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p));
        Project project = _workspace.AddProject("Component", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(references);
        _document = project.AddDocument("Example.razor.cs", SourceText.From("using System; using System.Threading.Tasks; using Microsoft.AspNetCore.Components;\n" + source));
    }

    [Given("another authored C# part is")]
    public void GivenAnotherPart(string source)
    {
        DocumentId original = _document!.Id;
        Project project = _document.Project.AddDocument("Other.cs", SourceText.From(
            "using Microsoft.AspNetCore.Components;\n" + source)).Project;
        _document = project.GetDocument(original)!;
    }

    [Then("each ordering diagnostic identifies its binding partner")]
    public void ThenPartnerLocations()
    {
        Assert.All(_diagnostics.Where(d => d.Id == ComponentMemberAnalyzer.OrderingId), d => Assert.Single(d.AdditionalLocations));
    }

    [When("component ordering Fix All is applied")]
    public async Task WhenFixAll()
    {
        ComponentMemberCodeFixProvider provider = new();
        FixAllContext context = new(_document!, provider, FixAllScope.Document, "OrderComponentMembers",
            provider.FixableDiagnosticIds, new ComponentDiagnosticProvider(), CancellationToken.None);
        CodeAction action = (await provider.GetFixAllProvider().GetFixAsync(context))!;
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation change = Assert.Single(operations.OfType<ApplyChangesOperation>());
        _document = change.ChangedSolution.GetDocument(_document!.Id)!;
    }

    [When("component conventions are checked")]
    public async Task WhenChecked()
    {
        Compilation compilation = (await _document!.Project.GetCompilationAsync())!;
        _diagnostics = await compilation.WithAnalyzers([new ComponentMemberAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(_diagnostics, d => d.Id == "AD0001");
        _actions.Clear();
        foreach (Diagnostic diagnostic in _diagnostics.Where(d => d.Id == ComponentMemberAnalyzer.OrderingId))
        {
            await new ComponentMemberCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(_document, diagnostic,
                (action, _) => _actions.Add(action), CancellationToken.None));
        }
    }

    [Then("there are {int} component ordering diagnostics")]
    public void ThenOrdering(int count) => Assert.Equal(count, _diagnostics.Count(d => d.Id == ComponentMemberAnalyzer.OrderingId));

    [Then("there are {int} nested component type diagnostics")]
    public void ThenNested(int count) => Assert.Equal(count, _diagnostics.Count(d => d.Id == ComponentMemberAnalyzer.NestedTypeId));

    [Then("there are {int} safe component ordering fixes")]
    public void ThenFixes(int count) => Assert.Equal(count, _actions.Count);

    [When("the safe component ordering fix is applied")]
    public async Task WhenFixed()
    {
        CodeAction action = Assert.Single(_actions);
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation change = Assert.Single(operations.OfType<ApplyChangesOperation>());
        _document = change.ChangedSolution.GetDocument(_document!.Id)!;
    }

    [Then("the component source contains {string}")]
    public async Task ThenContains(string text) => Assert.Contains(text, (await _document!.GetTextAsync()).ToString(), StringComparison.Ordinal);

    [Then("component member {string} appears before {string}")]
    public async Task ThenMemberOrder(string first, string second)
    {
        string text = (await _document!.GetTextAsync()).ToString();
        int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
        int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0 && secondIndex > firstIndex);
    }

    public void Dispose() => _workspace.Dispose();
}
