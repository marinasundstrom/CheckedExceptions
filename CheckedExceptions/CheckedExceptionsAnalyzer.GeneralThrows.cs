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
        SemanticModel semanticModel,
        AnalyzerOptions options,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        const string generalExceptionName = "Exception";
        const string generalExceptionNamespace = "System";

        foreach (var attribute in throwsAttributes)
        {
            foreach (var arg in attribute.ArgumentList?.Arguments ?? [])
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type, cancellationToken);
                    var type = typeInfo.Type as INamedTypeSymbol;

                    if (type is null)
                        continue;

                    var settings = GetAnalyzerSettings(options);

                    if (settings.BaseExceptionDeclaredDiagnosticEnabled)
                    {
                        if (type.Name == generalExceptionName &&
                            type.ContainingNamespace?.ToDisplayString() == generalExceptionNamespace)
                        {
                            reportDiagnostic(Diagnostic.Create(
                                RuleGeneralThrowDeclared,
                                typeOfExpr.Type.GetLocation(), // ✅ report precisely on typeof(Exception)
                                type.Name));
                        }
                    }
                }
            }
        }
    }

    #endregion
}