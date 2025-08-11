using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void CheckForRedundantThrowsDeclarationsHandledByDeclaredSuperClass(
        SymbolAnalysisContext context,
        ImmutableArray<AttributeData> throwsAttributes)
    {
        var declaredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var typeToArgMap = new Dictionary<INamedTypeSymbol, TypeOfExpressionSyntax>(SymbolEqualityComparer.Default);

        foreach (var attrData in throwsAttributes)
        {
            var syntaxRef = attrData.ApplicationSyntaxReference;
            if (syntaxRef?.GetSyntax(context.CancellationToken) is not AttributeSyntax attrSyntax)
                continue;

            var semanticModel = context.Compilation.GetSemanticModel(attrSyntax.SyntaxTree);

            foreach (var arg in attrSyntax.ArgumentList?.Arguments ?? default)
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                    var typeSymbol = typeInfo.Type as INamedTypeSymbol;
                    if (typeSymbol == null)
                        continue;

                    declaredTypes.Add(typeSymbol);
                    typeToArgMap[typeSymbol] = typeOfExpr; // more precise location
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
                    if (typeToArgMap.TryGetValue(type, out var expression))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleDuplicateThrowsByHierarchy,
                            expression.Type.GetLocation(), // ✅ precise location
                            otherType.Name));
                    }
                    break;
                }
            }
        }

        static bool IsSubclassOf(INamedTypeSymbol derived, INamedTypeSymbol baseType)
        {
            var current = derived.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }

    private static void CheckForRedundantThrowsHandledByDeclaredSuperClass(
     IEnumerable<AttributeSyntax> throwsAttributes,
     SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var declaredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var typeToExprMap = new Dictionary<INamedTypeSymbol, TypeOfExpressionSyntax>(SymbolEqualityComparer.Default);

        foreach (var throwsAttribute in throwsAttributes)
        {
            foreach (var arg in throwsAttribute.ArgumentList?.Arguments ?? default)
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                    var exceptionType = typeInfo.Type as INamedTypeSymbol;

                    if (exceptionType == null)
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
                        context.ReportDiagnostic(Diagnostic.Create(
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
            while (current != null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }
}