using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Methods

    private static void CheckForGeneralExceptionThrows(ImmutableArray<AttributeData> throwsAttributes, SymbolAnalysisContext context)
    {
        string exceptionName = "Exception";

        IEnumerable<AttributeData> generalExceptionAttributes = FilterThrowsAttributesByException(throwsAttributes, exceptionName);

        foreach (var attribute in generalExceptionAttributes)
        {
            // Report diagnostic for [Throws(typeof(Exception))]
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
            if (attributeSyntax != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(RuleGeneralThrows, attributeSyntax.GetLocation()));
            }
        }
    }

    #endregion

    #region Lambda expression and Local functions

    private void CheckForGeneralExceptionThrows(SyntaxNodeAnalysisContext context, List<AttributeSyntax> throwsAttributes)
    {
        string generalExceptionName = "Exception";

        // Check for general Throws(typeof(Exception)) attributes
        foreach (var attribute in throwsAttributes)
        {
            var exceptionTypeName = GetExceptionTypeName(attribute, context.SemanticModel);
            if (exceptionTypeName == generalExceptionName)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleGeneralThrows,
                    attribute.GetLocation(),
                    generalExceptionName));
            }
        }
    }

    #endregion
}