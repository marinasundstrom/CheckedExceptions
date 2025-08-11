using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Method

    private static void CheckForDuplicateThrowsDeclarations(
        SymbolAnalysisContext context,
        ImmutableArray<AttributeData> throwsAttributes)
    {
        var reportedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

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
                    var exceptionType = typeInfo.Type as INamedTypeSymbol;
                    if (exceptionType is null)
                        continue;

                    if (reportedTypes.Contains(exceptionType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleDuplicateDeclarations,
                            typeOfExpr.Type.GetLocation(), // ✅ precise location
                            exceptionType.Name));
                    }

                    reportedTypes.Add(exceptionType);
                }
            }
        }
    }

    #endregion

    #region Lambda expression & Local function

    /// <summary>
    /// Checks for duplicate [Throws] declarations of the same exception type and reports diagnostics at precise locations.
    /// </summary>
    /// <param name="throwsAttributes">The collection of ThrowsAttribute syntax nodes.</param>
    /// <param name="context">The analysis context.</param>
    private static void CheckForDuplicateThrowsDeclarations(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var throwsAttribute in throwsAttributes)
        {
            foreach (var arg in throwsAttribute.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                    var exceptionType = typeInfo.Type as INamedTypeSymbol;
                    if (exceptionType is null)
                        continue;

                    if (seen.Contains(exceptionType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleDuplicateDeclarations,
                            typeOfExpr.Type.GetLocation(), // ✅ precise location
                            exceptionType.Name));
                    }

                    seen.Add(exceptionType);
                }
            }
        }
    }

    #endregion
}