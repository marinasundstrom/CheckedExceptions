using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// Analyzes exceptions thrown by a method, constructor, or other member.
    /// </summary>
    private static void AnalyzeMemberExceptions(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol,
        AnalyzerSettings settings)
    {
        if (methodSymbol is null)
            return;

        if (!settings.IsLinqQueryableSupportEnabled && IsQueryableExtension(methodSymbol))
            return;

        var exceptionTypes = new HashSet<INamedTypeSymbol>(
            GetExceptionTypes(methodSymbol), SymbolEqualityComparer.Default);

        if (settings.IsXmlInteropEnabled)
        {
            // Get exceptions from XML documentation
            var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, methodSymbol);

            xmlExceptionTypes = ProcessNullable(context.Compilation, context.SemanticModel, node, methodSymbol, xmlExceptionTypes);

            if (xmlExceptionTypes.Any())
            {
                exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
            }
        }

        if (node is InvocationExpressionSyntax invocation)
        {
            if (settings.IsLinqSupportEnabled)
            {
                AnalyzeLinqOperation(context, methodSymbol, exceptionTypes, invocation);
            }
        }

        exceptionTypes = new HashSet<INamedTypeSymbol>(ProcessNullable(context.Compilation, context.SemanticModel, node, methodSymbol, exceptionTypes), SymbolEqualityComparer.Default);

        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, node, exceptionType, settings);
        }
    }
}