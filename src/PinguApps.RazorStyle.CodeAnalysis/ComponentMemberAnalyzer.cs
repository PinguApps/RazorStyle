using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.RazorStyle.CodeAnalysis;

/// <summary>Checks authored component declarations without imposing order between partial parts.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComponentMemberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The member ordering rule.</summary>
    public const string OrderingId = "PARS0004";

    /// <summary>The nested type rule.</summary>
    public const string NestedTypeId = "PARS0005";

    private static readonly DiagnosticDescriptor _ordering = new(
        OrderingId, "Order component members", "Order component members and keep binding callbacks immediately after their value parameter",
        "Layout", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor _nestedType = new(
        NestedTypeId, "Extract nested component type", "Extract '{0}' to its own file; preserve accessibility, references and generic context manually",
        "Layout", DiagnosticSeverity.Warning, true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_ordering, _nestedType];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        ClassDeclarationSyntax declaration = (ClassDeclarationSyntax)context.Node;
        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } component
            || !ComponentMemberOrder.IsComponent(component, context.Compilation))
        {
            return;
        }

        foreach (MemberDeclarationSyntax member in declaration.Members)
        {
            if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(_nestedType, member.GetLocation(),
                    context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken)?.Name ?? "nested type"));
            }
        }

        ComponentMemberOrder order = new(declaration, context.SemanticModel, context.CancellationToken);
        if (!order.IsOrdered)
        {
            context.ReportDiagnostic(Diagnostic.Create(_ordering, declaration.Identifier.GetLocation()));
        }

        foreach (Tuple<Location, Location> pair in order.ExternalPairs)
        {
            context.ReportDiagnostic(Diagnostic.Create(_ordering, pair.Item1,
                additionalLocations: [pair.Item2], properties: null));
        }
    }
}

