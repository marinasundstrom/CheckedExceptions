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

            foreach (var declared in declaredExceptions)
            {
                var isCompatible = baseExceptions.Any(baseEx =>
                    declared.Equals(baseEx, SymbolEqualityComparer.Default) ||
                    ((INamedTypeSymbol)declared).InheritsFrom((INamedTypeSymbol)baseEx));

                if (!isCompatible)
                {
                    var location = baseMethod.Locations.FirstOrDefault();
                    var memberName = $"{baseMethod.ContainingType.Name}.{baseMethod.Name}";

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
                    Console.WriteLine($"❌ Incompatible: {memberName} lacks {declared.Name}\n{location.GetLineSpan()}");
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

                    var diagnostic = Diagnostic.Create(
                        RuleMissingThrowsOnBaseMember,
                        location,
                        memberName,
                        declared.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
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