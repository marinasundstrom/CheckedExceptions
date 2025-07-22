using System.Collections.Immutable;

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

        var declaredExceptions = GetDistictExceptionTypes(throwsAttributes).ToImmutableHashSet(SymbolEqualityComparer.Default);

        if (declaredExceptions.Count == 0)
            return;

        var baseMethods = GetBaseOrInterfaceMethods(method)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>();

        foreach (var baseMethod in baseMethods)
        {
            var baseExceptions = GetExceptionTypes(baseMethod).ToImmutableHashSet(SymbolEqualityComparer.Default);

            AnalyzeMissingThrowsOnBaseMember(context, method, declaredExceptions, baseMethod, baseExceptions);

            AnalyzeMissingThrowsFromBaseMember(context, method, declaredExceptions, baseMethod, baseExceptions);
        }
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
                var location = method.Locations.FirstOrDefault();
                var baseName = FormatMethodSignature(baseMethod);

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsFromBaseMember,
                    location,
                    baseName,
                    baseException.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
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
                var location = method.Locations.FirstOrDefault();
                var memberName = FormatMethodSignature(baseMethod);

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsOnBaseMember,
                    location,
                    memberName,
                    declared.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
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