using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region Lambda expression & Local function

    /// <summary>
    /// Checks for duplicate [Throws] declarations of the same exception type and reports diagnostics at precise locations.
    /// </summary>
    /// <param name="throwsAttributes">The collection of ThrowsAttribute syntax nodes.</param>
    /// <param name="context">The analysis context.</param>
    private static void CheckForDuplicateThrowsDeclarations(
        IEnumerable<AttributeSyntax> throwsAttributes,
        ThrowsContext context)
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