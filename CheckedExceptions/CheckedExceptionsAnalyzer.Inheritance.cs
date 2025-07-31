using System.Collections.Immutable;
using System.Net.NetworkInformation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Method

    private void CheckForCompatibilityWithBaseOrInterface(SymbolAnalysisContext context, ImmutableArray<AttributeData> throwsAttributes)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.MethodKind is not (
            MethodKind.Ordinary or
            MethodKind.PropertyGet or
            MethodKind.PropertySet or
            MethodKind.EventAdd or
            MethodKind.EventRemove))
            return;

        ImmutableHashSet<ISymbol?> declaredExceptions;

        if (IsExpressionBodiedProperty(method, out var property))
        {
            // Handle the case when the property decl has throws declaration and only a set accessor.
            // Should be treated as if the Throws is on the get accessor

            declaredExceptions = GetDistictExceptionTypes(property!.GetAttributes()).ToImmutableHashSet(SymbolEqualityComparer.Default);
        }
        else
        {
            declaredExceptions = GetDistictExceptionTypes(throwsAttributes).ToImmutableHashSet(SymbolEqualityComparer.Default);
        }

        //if (declaredExceptions.Count == 0)
        //    return;

        var baseMethods = GetBaseOrInterfaceMethods(method)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>();

        foreach (var baseMethod in baseMethods)
        {
            ImmutableHashSet<ISymbol?> baseExceptions;

            if (IsExpressionBodiedProperty(baseMethod, out var baseProperty))
            {
                // Handle the case when base property decl has throws declaration and only a set accessor.
                // Should be treated as if the Throws is on the get accessor

                baseExceptions = GetExceptionTypes(baseProperty!).ToImmutableHashSet(SymbolEqualityComparer.Default);
            }
            else
            {
                baseExceptions = GetExceptionTypes(baseMethod).ToImmutableHashSet(SymbolEqualityComparer.Default);
            }

            AnalyzeMissingThrowsOnBaseMember(context, method, declaredExceptions, baseMethod, baseExceptions);

            AnalyzeMissingThrowsFromBaseMember(context, method, declaredExceptions, baseMethod, baseExceptions);
        }
    }

    private bool IsExpressionBodiedProperty(IMethodSymbol method, out IPropertySymbol? propertySymbol)
    {
        if (method.MethodKind is MethodKind.PropertyGet
                    && method.AssociatedSymbol is IPropertySymbol prop
                    && prop.GetMethod is not null && prop.SetMethod is null
                    && HasThrowsAttributes(prop))
        {
            propertySymbol = prop;
            return true;
        }

        propertySymbol = null;
        return false;
    }

    private bool HasThrowsAttributes(ISymbol symbol)
    {
        return GetThrowsAttributes(symbol).Any();
    }

    private void AnalyzeMissingThrowsFromBaseMember(SymbolAnalysisContext context, IMethodSymbol method, ImmutableHashSet<ISymbol?> declaredExceptions, IMethodSymbol baseMethod, ImmutableHashSet<ISymbol?> baseExceptions)
    {
        foreach (var baseException in baseExceptions.OfType<ITypeSymbol>())
        {
            // Skip if base exception is System.Exception or a base class thereof
            if (IsTooGenericException(baseException))
                continue;

            var isCovered = declaredExceptions.Any(declared =>
            {
                if (declared.Equals(baseException, SymbolEqualityComparer.Default))
                    return true;

                var declaredNamed = declared as INamedTypeSymbol;
                var baseNamed = baseException as INamedTypeSymbol;

                return declaredNamed != null && baseNamed != null && declaredNamed.InheritsFrom(baseNamed);
            });

            if (!isCovered)
            {
                var baseName = FormatMethodSignature(baseMethod);

                var properties = ImmutableDictionary.Create<string, string?>()
                .Add("ExceptionType", baseException.Name);

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsFromBaseMember,
                    GetSourceLocationForTarget(method),
                    properties,
                    baseName,
                    baseException.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private Location? GetSourceLocationForTarget(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.AssociatedSymbol is IPropertySymbol propertySymbol)
        {
            var syntaxReference = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault();

            if (syntaxReference is not null)
            {
                var propertyDeclaration = syntaxReference.GetSyntax() as PropertyDeclarationSyntax;
                if (propertyDeclaration is not null && propertyDeclaration.ExpressionBody is not null)
                {
                    return propertyDeclaration.GetLocation();
                }
            }
        }

        return methodSymbol.Locations.FirstOrDefault();
    }

    private static void AnalyzeMissingThrowsOnBaseMember(SymbolAnalysisContext context, IMethodSymbol method, ImmutableHashSet<ISymbol?> declaredExceptions, IMethodSymbol baseMethod, ImmutableHashSet<ISymbol?> baseExceptions)
    {
        foreach (var declared in declaredExceptions)
        {
            var isCompatible = baseExceptions.Any(baseEx =>
                declared.Equals(baseEx, SymbolEqualityComparer.Default) ||
                ((INamedTypeSymbol)declared).InheritsFrom((INamedTypeSymbol)baseEx));

            if (!isCompatible)
            {
                // TODO: Would be lovely if we could get the location of "typeof(ExceptionType)"
                //.      That is the exception in "declared".

                var location = method.Locations.FirstOrDefault();
                var memberName = FormatMethodSignature(baseMethod);

                // Abort - since the diagnostic would be unhelpful when name is empty.
                if (string.IsNullOrEmpty(declared!.Name))
                    continue;

                var properties = ImmutableDictionary.Create<string, string?>()
                    .Add("ExceptionType", declared.Name);

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsOnBaseMember,
                    GetLocationOfExceptionNameInTypeOfInThrowsAttribute(context.Compilation, method, declared),
                    properties,
                    memberName,
                    declared.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static Location? GetLocationOfExceptionNameInTypeOfInThrowsAttribute(
        Compilation compilation, IMethodSymbol method, ISymbol declared)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var syntaxNode = syntaxRef.GetSyntax();

            // We care about methods and accessors
            if (syntaxNode is not (BaseMethodDeclarationSyntax or AccessorDeclarationSyntax))
                continue;

            SyntaxList<AttributeListSyntax> attributeLists =
                syntaxNode switch
                {
                    BaseMethodDeclarationSyntax m => m.AttributeLists,
                    AccessorDeclarationSyntax a => a.AttributeLists,
                    _ => default
                };

            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    // Only consider [Throws]
                    var name = attribute.Name.ToString();
                    if (name is not ("Throws" or "ThrowsAttribute"))
                        continue;

                    if (attribute.ArgumentList is null)
                        continue;

                    foreach (var arg in attribute.ArgumentList.Arguments)
                    {
                        if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                        {
                            var typeSyntax = typeOfExpr.Type;
                            var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                            var typeSymbol = semanticModel.GetTypeInfo(typeSyntax).Type;

                            if (SymbolEqualityComparer.Default.Equals(typeSymbol, declared))
                            {
                                // ✅ Point directly at the type name inside typeof(...)
                                return typeSyntax.GetLocation();
                            }
                        }
                    }
                }
            }
        }

        // Fallback: method name location
        return method.Locations.FirstOrDefault();
    }

    public static string FormatMethodSignature(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        var methodName = methodSymbol.Name;
        var parameters = string.Join(", ", methodSymbol.Parameters.Select(p =>
            p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

        return $"{typeName}.{methodName}({parameters})";
    }

    private bool IsTooGenericException(ITypeSymbol ex)
    {
        var namedType = ex as INamedTypeSymbol;
        if (namedType == null)
            return false;

        var fullName = namedType.ToDisplayString();

        return fullName == "System.Exception" || fullName == "System.SystemException";
    }

    private IEnumerable<IMethodSymbol> GetBaseOrInterfaceMethods(IMethodSymbol method)
    {
        var results = new List<IMethodSymbol>();

        if (method.OverriddenMethod is not null)
            results.Add(method.OverriddenMethod);

        if (method.AssociatedSymbol is IPropertySymbol prop && prop.OverriddenProperty is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(method, prop.GetMethod))
                results.Add(prop.OverriddenProperty.GetMethod);
            else if (SymbolEqualityComparer.Default.Equals(method, prop.SetMethod))
                results.Add(prop.OverriddenProperty.SetMethod);
        }

        if (method.AssociatedSymbol is IEventSymbol ev && ev.OverriddenEvent is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(method, ev.AddMethod))
                results.Add(ev.OverriddenEvent.AddMethod);
            else if (SymbolEqualityComparer.Default.Equals(method, ev.RemoveMethod))
                results.Add(ev.OverriddenEvent.RemoveMethod);
        }

        var type = method.ContainingType;
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers().OfType<IMethodSymbol>())
            {
                var impl = type.FindImplementationForInterfaceMember(ifaceMember) as IMethodSymbol;
                if (impl != null && impl.Equals(method, SymbolEqualityComparer.Default))
                {
                    results.Add(ifaceMember);
                }
            }
        }

        return results;
    }

    #endregion
}