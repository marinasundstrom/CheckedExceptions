using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
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

        ImmutableHashSet<ISymbol> declaredExceptions = GetDistinctExceptionTypes(throwsAttributes).Where(x => x is not null).ToImmutableHashSet(SymbolEqualityComparer.Default)!;
        Debug.Assert(!declaredExceptions.Any(x => x is null));

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

    private void AnalyzeMissingThrowsFromBaseMember(SymbolAnalysisContext context, IMethodSymbol method, ImmutableHashSet<ISymbol> declaredExceptions, IMethodSymbol baseMethod, ImmutableHashSet<ISymbol?> baseExceptions)
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
                var baseName = $"{baseMethod.ContainingType.Name}.{baseMethod.Name}";

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsFromBaseMember,
                    location,
                    baseName,
                    baseException.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeMissingThrowsOnBaseMember(SymbolAnalysisContext context, IMethodSymbol method, ImmutableHashSet<ISymbol> declaredExceptions, IMethodSymbol baseMethod, ImmutableHashSet<ISymbol?> baseExceptions)
    {
        foreach (var declared in declaredExceptions)
        {
            var isCompatible = baseExceptions.Any(baseEx => baseEx is not null &&
                (declared.Equals(baseEx, SymbolEqualityComparer.Default)
                || ((INamedTypeSymbol)declared).InheritsFrom((INamedTypeSymbol)baseEx)));

            if (!isCompatible)
            {
                var location = method.Locations.FirstOrDefault();
                var memberName = $"{baseMethod.ContainingType.Name}.{baseMethod.Name}";

                var diagnostic = Diagnostic.Create(
                    RuleMissingThrowsOnBaseMember,
                    location,
                    memberName,
                    declared.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private bool IsTooGenericException(ITypeSymbol ex)
    {
        if (ex is not INamedTypeSymbol namedTypeSymbol) return false;

        var fullName = namedTypeSymbol.ToDisplayString();

        return fullName.Equals(typeof(Exception).FullName, StringComparison.Ordinal) || fullName.Equals(typeof(SystemException).FullName, StringComparison.Ordinal);
    }

    private IEnumerable<IMethodSymbol> GetBaseOrInterfaceMethods(IMethodSymbol method)
    {
        var results = new List<IMethodSymbol>();

        if (method.OverriddenMethod is not null)
            results.Add(method.OverriddenMethod);

        if (method.AssociatedSymbol is IPropertySymbol prop && prop.OverriddenProperty is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(method, prop.GetMethod) && prop.OverriddenProperty.GetMethod is { } getMethodSymbol)
                results.Add(getMethodSymbol);
            else if (SymbolEqualityComparer.Default.Equals(method, prop.SetMethod) && prop.OverriddenProperty.SetMethod is { } setMethodSymbol)
                results.Add(setMethodSymbol);
        }

        if (method.AssociatedSymbol is IEventSymbol ev && ev.OverriddenEvent is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(method, ev.AddMethod) && ev.OverriddenEvent.AddMethod is { } addMethodSymbol)
                results.Add(addMethodSymbol);
            else if (SymbolEqualityComparer.Default.Equals(method, ev.RemoveMethod) && ev.OverriddenEvent.RemoveMethod is { } removeMethodSymbol)
                results.Add(removeMethodSymbol);
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
}