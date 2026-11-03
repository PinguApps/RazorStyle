using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PinguApps.RazorStyle.CodeAnalysis;

internal sealed class ComponentMemberOrder
{
    private static readonly string[] _lifecycle = ["OnInitialized", "OnInitializedAsync", "OnParametersSet", "OnParametersSetAsync", "ShouldRender", "OnAfterRender", "OnAfterRenderAsync"];
    private readonly SemanticModel _model;
    private readonly INamedTypeSymbol _component;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<IPropertySymbol, IPropertySymbol> _pairs = new(SymbolEqualityComparer.Default);
    private readonly ClassDeclarationSyntax _declaration;

    public ComponentMemberOrder(ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellationToken)
    {
        _declaration = declaration;
        _model = model;
        _cancellationToken = cancellationToken;
        _component = _model.GetDeclaredSymbol(_declaration, _cancellationToken)!;
        IPropertySymbol[] parameters = [.. _component.GetMembers().OfType<IPropertySymbol>().Where(p => HasAttribute(p, "Parameter") && !HasAttribute(p, "CascadingParameter") && !p.IsStatic)];
        foreach (IPropertySymbol value in parameters)
        {
            IPropertySymbol[] callbacks = [.. parameters.Where(p => p.Name == value.Name + "Changed"
                && p.Type is INamedTypeSymbol named
                && value.Type.TypeKind != TypeKind.Error
                && (IsFramework(named, "EventCallback") || (IsFramework(named, "EventCallback`1")
                && SymbolEqualityComparer.IncludeNullability.Equals(named.TypeArguments[0], value.Type))))];
            if (callbacks.Length == 1 && parameters.Count(p => p.Name == value.Name) == 1
                && !_pairs.ContainsKey(callbacks[0]) && !_pairs.ContainsValue(callbacks[0]) && !_pairs.ContainsValue(value))
            {
                _pairs.Add(value, callbacks[0]);
            }
        }

        MemberDeclarationSyntax[] members = [.. _declaration.Members.Where(m => m is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax)];
        Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> localPairs = [];
        foreach (KeyValuePair<IPropertySymbol, IPropertySymbol> pair in _pairs)
        {
            PropertyDeclarationSyntax? value = members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => Same(_model.GetDeclaredSymbol(p, _cancellationToken), pair.Key));
            PropertyDeclarationSyntax? callback = members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => Same(_model.GetDeclaredSymbol(p, _cancellationToken), pair.Value));
            if (value is not null && callback is not null)
            {
                localPairs.Add(value, callback);
            }
            else if (value is not null || callback is not null)
            {
                Location? first = (value ?? callback)?.GetLocation();
                ISymbol other = value is not null ? pair.Value : pair.Key;
                Location? second = other.Locations.FirstOrDefault(l => l.IsInSource);
                if (first is not null && second is not null)
                {
                    ExternalPairs.Add((first, second, pair.Key.Name, pair.Value.Name));
                }
            }
        }

        OrderedMembers = members.Where(m => !localPairs.ContainsValue(m))
            .OrderBy(Rank).SelectMany(m => localPairs.TryGetValue(m, out MemberDeclarationSyntax? callback)
                ? new[] { m, callback } : [m]).ToArray();
        IsOrdered = members.SequenceEqual(OrderedMembers);
        for (int index = 0; index < members.Length; index++)
        {
            if (members[index] != OrderedMembers[index])
            {
                OrderingCorrection = $"Move '{MemberName(OrderedMembers[index])}' before '{MemberName(members[index])}'.";
                break;
            }
        }
    }

    public IReadOnlyList<MemberDeclarationSyntax> OrderedMembers
    {
        get;
    }

    public List<(Location Member, Location Partner, string ValueName, string CallbackName)> ExternalPairs { get; } = [];

    public string OrderingCorrection { get; } = string.Empty;

    public bool IsOrdered
    {
        get;
    }

    public bool CanFix => ExternalPairs.Count == 0 && !_declaration.ContainsDiagnostics
        && !_model.GetDiagnostics(_declaration.Span, _cancellationToken).Any(d => d.Severity == DiagnosticSeverity.Error)
        && !_declaration.DescendantTrivia(descendIntoTrivia: true).Any(t => t.IsDirective)
        && !_declaration.Members.Any(m => m is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
        && _declaration.Members.Where(HasInitializer).SequenceEqual(OrderedMembers.Where(HasInitializer));

    public static bool IsComponent(INamedTypeSymbol symbol, Compilation compilation)
    {
        INamedTypeSymbol? framework = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ComponentBase");
        for (INamedTypeSymbol? current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (framework is not null && framework.ContainingAssembly.Name == "Microsoft.AspNetCore.Components"
                && Same(current.OriginalDefinition, framework))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Same(ISymbol? left, ISymbol? right) => SymbolEqualityComparer.Default.Equals(left, right);

    private string MemberName(MemberDeclarationSyntax member) => member is BaseFieldDeclarationSyntax field
        ? string.Join(", ", field.Declaration.Variables.Select(variable => variable.Identifier.ValueText))
        : _model.GetDeclaredSymbol(member, _cancellationToken)?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? member.Kind().ToString();

    private static bool HasInitializer(MemberDeclarationSyntax member) => member switch
    {
        BaseFieldDeclarationSyntax field => field.Declaration.Variables.Any(v => v.Initializer is not null),
        PropertyDeclarationSyntax property => property.Initializer is not null,
        _ => false,
    };

    private bool IsFramework(INamedTypeSymbol type, string name) => Same(type.OriginalDefinition,
        _model.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components." + name));

    private bool HasAttribute(IPropertySymbol property, string name) => property.GetAttributes().Any(a =>
        a.AttributeClass is not null && IsFramework(a.AttributeClass, name + "Attribute"));

    private int Rank(MemberDeclarationSyntax member)
    {
        ISymbol? symbol = _model.GetDeclaredSymbol(member, _cancellationToken);
        if (symbol is IPropertySymbol property)
        {
            if (HasAttribute(property, "CascadingParameter"))
            {
                return 0;
            }

            if (HasAttribute(property, "Parameter"))
            {
                IPropertySymbol value = _pairs.FirstOrDefault(p => Same(p.Value, property)).Key ?? property;
                if (HasAttribute(value, "EditorRequired"))
                {
                    return 100;
                }

                if (_pairs.ContainsKey(value))
                {
                    return 200;
                }

                if (property.Type is INamedTypeSymbol named)
                {
                    if (IsFramework(named, "EventCallback") || IsFramework(named, "EventCallback`1"))
                    {
                        return 300;
                    }

                    if (IsFramework(named, "RenderFragment") || IsFramework(named, "RenderFragment`1"))
                    {
                        return 400;
                    }
                }

                return 200;
            }

            return 700;
        }

        if (member is FieldDeclarationSyntax)
        {
            return 500;
        }
        if (member is ConstructorDeclarationSyntax)
        {
            return 600;
        }
        if (member is EventDeclarationSyntax or EventFieldDeclarationSyntax)
        {
            return 800;
        }
        if (symbol is IMethodSymbol method)
        {
            foreach (string contract in new[] { "System.IDisposable", "System.IAsyncDisposable" })
            {
                INamedTypeSymbol? type = _model.Compilation.GetTypeByMetadataName(contract);
                if (type is null || type.TypeKind != TypeKind.Interface || type.DeclaringSyntaxReferences.Length != 0)
                {
                    continue;
                }
                foreach (ISymbol slot in type.GetMembers())
                {
                    ISymbol? implementation = _component.FindImplementationForInterfaceMember(slot);
                    for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
                    {
                        if (Same(current, implementation))
                        {
                            return 1100;
                        }
                    }
                }
            }

            for (IMethodSymbol? current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
            {
                if (Same(current.ContainingType, _model.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ComponentBase")))
                {
                    int index = Array.IndexOf(_lifecycle, current.Name);
                    if (index >= 0)
                    {
                        return 900 + index;
                    }
                }
            }
        }

        return 1000;
    }
}
