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
        OrderingId, "Order component members", "{0} Expected groups: "
        + "1 cascading parameters; 2 editor-required parameter groups; 3 optional parameter groups; "
        + "4 standalone callback parameters; 5 RenderFragment/RenderFragment<T> content parameters; "
        + "6 constants and fields; 7 constructors; 8 other properties; 9 .NET events; "
        + "10 lifecycle overrides; 11 other methods; 12 Dispose/DisposeAsync implementations. "
        + "Keep each binding pair X/XChanged together: XChanged immediately follows X and takes X's required/optional group. "
        + "Lifecycle order: OnInitialized(), OnInitializedAsync(), OnParametersSet(), OnParametersSetAsync(), "
        + "ShouldRender(), OnAfterRender(bool), OnAfterRenderAsync(bool). Preserve existing relative order within other groups. "
        + "When correcting layout manually, preserve initializer execution order and directive/partial boundaries.",
        "Layout", DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/PinguApps/RazorStyle#pars0004-component-member-order");

    private static readonly DiagnosticDescriptor _nestedType = new(
        NestedTypeId, "Extract nested component type", "Nested type '{0}' is not allowed in a component code-behind. "
        + "Extract it to its own top-level file in the appropriate namespace; top-level types cannot be private. "
        + "Preserve accessibility, references, public API and generic context manually.",
        "Layout", DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/PinguApps/RazorStyle#pars0005-no-nested-component-types");

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
            context.ReportDiagnostic(Diagnostic.Create(_ordering, declaration.Identifier.GetLocation(), order.OrderingCorrection));
        }

        foreach ((Location member, Location partner, string valueName, string callbackName) in order.ExternalPairs)
        {
            FileLinePositionSpan partnerPosition = partner.GetLineSpan();
            string correction = $"Binding pair '{valueName}/{callbackName}' is split across partial declarations; "
                + $"the partner is at {partnerPosition.Path}:{partnerPosition.StartLinePosition.Line + 1}. "
                + $"Manually place '{callbackName}' immediately after '{valueName}' in the same declaration; no cross-part automatic fix is offered.";
            context.ReportDiagnostic(Diagnostic.Create(_ordering, member,
                additionalLocations: [partner], properties: null, messageArgs: [correction]));
        }
    }
}
