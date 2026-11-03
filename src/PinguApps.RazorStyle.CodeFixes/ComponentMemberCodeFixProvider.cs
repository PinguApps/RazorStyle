using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PinguApps.RazorStyle.CodeAnalysis;

/// <summary>Reorders safe component declarations, retaining member trivia and initialization order.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ComponentMemberCodeFixProvider)), Shared]
public sealed class ComponentMemberCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [ComponentMemberAnalyzer.OrderingId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        ClassDeclarationSyntax? declaration = root?.FindToken(context.Span.Start).Parent?.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || model is null
            || model.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } component
            || !ComponentMemberOrder.IsComponent(component, model.Compilation))
        {
            return;
        }
        ComponentMemberOrder order = new(declaration, model, context.CancellationToken);
        if (!order.CanFix || order.IsOrdered)
        {
            return;
        }
        context.RegisterCodeFix(CodeAction.Create("Order component members", _ => Task.FromResult(
            context.Document.WithSyntaxRoot(root!.ReplaceNode(declaration,
                declaration.WithMembers(SyntaxFactory.List(order.OrderedMembers))))), "OrderComponentMembers"), context.Diagnostics);
    }
}
