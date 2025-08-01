using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Methods

    private static void CheckForGeneralExceptionThrowDeclarations(
        ImmutableArray<AttributeData> throwsAttributes,
        SymbolAnalysisContext context)
    {
        const string exceptionName = "Exception";

        foreach (var attribute in throwsAttributes)
        {
            var syntaxRef = attribute.ApplicationSyntaxReference;
            if (syntaxRef?.GetSyntax(context.CancellationToken) is not AttributeSyntax attrSyntax)
                continue;

            var semanticModel = context.Compilation.GetSemanticModel(attrSyntax.SyntaxTree);

            foreach (var arg in attrSyntax.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                    var type = typeInfo.Type as INamedTypeSymbol;
                    if (type is null)
                        continue;

                    if (type.Name == exceptionName && type.ContainingNamespace?.ToDisplayString() == "System")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleGeneralThrows,
                            typeOfExpr.Type.GetLocation(), // ✅ precise location
                            type.Name));
                    }
                }
            }
        }
    }

    #endregion

    #region Lambda expression and Local functions

    private void CheckForGeneralExceptionThrows(
     SyntaxNodeAnalysisContext context,
     List<AttributeSyntax> throwsAttributes)
    {
        const string generalExceptionName = "Exception";
        const string generalExceptionNamespace = "System";

        var semanticModel = context.SemanticModel;

        foreach (var attribute in throwsAttributes)
        {
            foreach (var arg in attribute.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
                    var type = typeInfo.Type as INamedTypeSymbol;

                    if (type is null)
                        continue;

                    if (type.Name == generalExceptionName &&
                        type.ContainingNamespace?.ToDisplayString() == generalExceptionNamespace)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleGeneralThrows,
                            typeOfExpr.Type.GetLocation(), // ✅ report precisely on typeof(Exception)
                            type.Name));
                    }
                }
            }
        }
    }

    #endregion
}