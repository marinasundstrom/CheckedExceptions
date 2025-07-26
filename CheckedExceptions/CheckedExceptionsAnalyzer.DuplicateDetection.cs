using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    #region  Method

    private void CheckForDuplicateThrowsAttributes(SymbolAnalysisContext context, ImmutableArray<AttributeData> throwsAttributes)
    {
        HashSet<INamedTypeSymbol> exceptionTypesList = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var throwsAttribute in throwsAttributes)
        {
            var exceptionTypes = GetExceptionTypes(throwsAttribute);
            foreach (var exceptionType in exceptionTypes)
            {
                if (exceptionTypesList.FirstOrDefault(x => x.Equals(exceptionType, SymbolEqualityComparer.Default)) is not null)
                {
                    var duplicateAttributeSyntax = throwsAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
                    if (duplicateAttributeSyntax is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateDeclarations, duplicateAttributeSyntax.GetLocation(), exceptionType.Name));
                    }
                }

                exceptionTypesList.Add(exceptionType);
            }
        }
    }

    #endregion

    #region Lambda expression

    /// <summary>
    /// Checks for duplicate ThrowsAttributes declaring the same exception type and reports diagnostics.
    /// </summary>
    /// <param name="throwsAttributes">The collection of ThrowsAttribute instances.</param>
    /// <param name="context">The analysis context.</param>
    private void CheckForDuplicateThrowsDeclarations(IEnumerable<AttributeSyntax> throwsAttributes, SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;

        HashSet<INamedTypeSymbol> exceptionTypesList = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var throwsAttribute in throwsAttributes)
        {
            var exceptionTypes = GetExceptionTypes(throwsAttribute, semanticModel);
            foreach (var exceptionType in exceptionTypes)
            {
                if (exceptionTypesList.FirstOrDefault(x => x.Equals(exceptionType, SymbolEqualityComparer.Default)) is not null)
                {
                    var duplicateAttributeSyntax = throwsAttribute;
                    if (duplicateAttributeSyntax is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateDeclarations, duplicateAttributeSyntax.GetLocation(), exceptionType.Name));
                    }
                }

                exceptionTypesList.Add(exceptionType);
            }
        }
    }

    #endregion
}