using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Methods

    private static void CheckForGeneralExceptionThrows(
        ImmutableArray<AttributeData> throwsAttributes,
        SymbolAnalysisContext context)
    {
        foreach (var attribute in throwsAttributes)
        {
            var syntaxRef = attribute.ApplicationSyntaxReference;
            if (syntaxRef?.GetSyntax(context.CancellationToken) is not AttributeSyntax attrSyntax)
                continue;

            var semanticModel = context.Compilation.GetSemanticModel(attrSyntax.SyntaxTree);

            foreach (var arg in attrSyntax.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                    continue;

                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                if (typeInfo.Type is not INamedTypeSymbol type)
                    continue;

                if (nameof(Exception).Equals(type.Name, StringComparison.Ordinal) && nameof(System).Equals(type.ContainingNamespace?.ToDisplayString(), StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleGeneralThrows,
                        typeOfExpr.GetLocation(), // ✅ precise location
                        type.Name));
                }
            }
        }
    }

    #endregion

    #region Lambda expression and Local functions

    private void CheckForGeneralExceptionThrows(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;

        foreach (var attribute in throwsAttributes)
        {
            foreach (var arg in attribute.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                    continue;

                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                if (typeInfo.Type is not INamedTypeSymbol type)
                    continue;

                if (nameof(Exception).Equals(type.Name, StringComparison.Ordinal) &&
                    nameof(System).Equals(type.ContainingNamespace?.ToDisplayString(), StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleGeneralThrows,
                        typeOfExpr.GetLocation(), // ✅ report precisely on typeof(Exception)
                        type.Name));
                }
            }
        }
    }

    #endregion
}