using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void CheckForRedundantThrowsHandledByDeclaredSuperClass(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        var declaredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var typeToExprMap = new Dictionary<INamedTypeSymbol, TypeOfExpressionSyntax>(SymbolEqualityComparer.Default);

        foreach (var throwsAttribute in throwsAttributes)
        {
            foreach (var arg in throwsAttribute.ArgumentList?.Arguments ?? default)
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, cancellationToken);
                    var exceptionType = typeInfo.Type as INamedTypeSymbol;

                    if (exceptionType is null)
                        continue;

                    declaredTypes.Add(exceptionType);
                    typeToExprMap[exceptionType] = typeOfExpr;
                }
            }
        }

        foreach (var type in declaredTypes)
        {
            foreach (var otherType in declaredTypes)
            {
                if (type.Equals(otherType, SymbolEqualityComparer.Default))
                    continue;

                if (IsSubclassOf(type, otherType))
                {
                    if (typeToExprMap.TryGetValue(type, out var expr))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            RuleDuplicateThrowsByHierarchy,
                            expr.Type.GetLocation(),
                            otherType.Name));
                    }
                    break;
                }
            }
        }

        static bool IsSubclassOf(INamedTypeSymbol derived, INamedTypeSymbol baseType)
        {
            var current = derived.BaseType;
            while (current is not null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }
}