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
        var duplicateGroups = throwsAttributes
            .GroupBy(attr => GetExceptionTypeName(attr))
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            // Skip the first occurrence and report duplicates
            foreach (var duplicateAttribute in group.Skip(1))
            {
                var duplicateAttributeSyntax = duplicateAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
                if (duplicateAttributeSyntax != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateThrow, duplicateAttributeSyntax.GetLocation(), group.Key));
                }
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
    private void CheckForDuplicateThrowsAttributes(IEnumerable<AttributeSyntax> throwsAttributes, SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;

        // Extract exception type names for each ThrowsAttribute
        var duplicateGroups = throwsAttributes
            .GroupBy(attr => GetExceptionTypeName(attr, semanticModel))
            .Where(group => group.Count() > 1)
            .ToList();


        foreach (var duplicateGroup in duplicateGroups)
        {
            // Identify all Attributes with this exception type
            var duplicateAttributes = duplicateGroup.ToList();

            // Skip the first occurrence and report duplicates
            foreach (var duplicateAttribute in duplicateAttributes.Skip(1))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleDuplicateThrow,
                    duplicateAttribute.GetLocation(),
                    duplicateGroup.Key));
            }
        }
    }

    #endregion
}