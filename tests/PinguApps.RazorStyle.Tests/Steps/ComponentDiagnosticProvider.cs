using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using PinguApps.RazorStyle.CodeAnalysis;

namespace PinguApps.RazorStyle.Tests.Steps;

internal sealed class ComponentDiagnosticProvider : FixAllContext.DiagnosticProvider
{
    public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
    {
        SyntaxTree? tree = await document.GetSyntaxTreeAsync(cancellationToken);
        return (await GetAllDiagnosticsAsync(document.Project, cancellationToken)).Where(d => d.Location.SourceTree == tree);
    }

    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<Diagnostic>());

    public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        Compilation compilation = (await project.GetCompilationAsync(cancellationToken))!;
        return await compilation.WithAnalyzers([new ComponentMemberAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }
}
