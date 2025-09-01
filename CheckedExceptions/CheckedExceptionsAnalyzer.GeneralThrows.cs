using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region Lambda expression and Local functions

    private static void CheckForGeneralExceptionThrows(
        IEnumerable<AttributeSyntax> throwsAttributes,
        ThrowsContext context)
    {
        const string generalExceptionName = "Exception";
        const string generalExceptionNamespace = "System";

        var semanticModel = context.SemanticModel;
        var settings = GetAnalyzerSettings(context.Options);

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

                    if (settings.BaseExceptionDeclaredDiagnosticEnabled &&
                        !settings.TreatThrowsExceptionAsCatchRestEnabled &&
                        type.Name == generalExceptionName &&
                        type.ContainingNamespace?.ToDisplayString() == generalExceptionNamespace)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleGeneralThrowDeclared,
                            typeOfExpr.Type.GetLocation(), // ✅ report precisely on typeof(Exception)
                            type.Name));
                    }
                }
            }
        }
    }

    #endregion
}