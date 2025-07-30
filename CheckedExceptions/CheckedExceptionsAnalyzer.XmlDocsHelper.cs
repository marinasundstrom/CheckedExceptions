using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private void CheckXmlDocsForUndeclaredExceptions(
        IEnumerable<AttributeData> throwsAttributes,
        SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var compilation = context.Compilation;

        // Gather existing [Throws] types
        var declaredExceptions = GetExceptionTypes(throwsAttributes)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        CheckXmlDocsForUndeclaredExceptionsCore(declaredExceptions, methodSymbol, compilation, context.ReportDiagnostic);
    }

    private void CheckXmlDocsForUndeclaredExceptions(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var methodSymbol = semanticModel.GetDeclaredSymbol(context.Node) as IMethodSymbol;

        if (methodSymbol is null)
            return;

        var compilation = context.Compilation;

        // Gather existing [Throws] types
        var declaredExceptions = throwsAttributes.Select(throwsAttribute => GetExceptionTypes(throwsAttribute, semanticModel))
            .SelectMany(x => x)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        CheckXmlDocsForUndeclaredExceptionsCore(declaredExceptions, methodSymbol!, compilation, context.ReportDiagnostic);
    }

    private void CheckXmlDocsForUndeclaredExceptionsCore(IEnumerable<ISymbol?> throwsAttributes, IMethodSymbol methodSymbol, Compilation compilation, Action<Diagnostic> reportDiagnostic)
    {
        // Parse <exception cref="..."/>
        var xmlDocExceptions = GetExceptionTypesFromDocumentationCommentXml_Syntax(compilation, methodSymbol);

        if (xmlDocExceptions.Count() == 0)
            return;

        // Find differences: XML-declared but not in [Throws]
        foreach (var exceptionInfo in xmlDocExceptions)
        {
            if (exceptionInfo.ExceptionType is null)
                continue;

            if (!throwsAttributes.Contains(exceptionInfo.ExceptionType, SymbolEqualityComparer.Default))
            {
                var properties = ImmutableDictionary.Create<string, string?>()
                    .Add("ExceptionType", exceptionInfo.ExceptionType.Name);

                var diag = Diagnostic.Create(
                    RuleXmlDocButNoThrows,
                    methodSymbol.Locations.FirstOrDefault() ?? Location.None,
                    properties,
                    exceptionInfo.ExceptionType.Name);

                reportDiagnostic(diag);
            }
        }
    }

    private IEnumerable<ExceptionInfo> GetExceptionTypesFromDocumentationCommentXml_Syntax(
        Compilation compilation,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken = default)
    {
        var xElement = GetDocCommentXml(methodSymbol, cancellationToken);
        if (xElement is null)
            return Enumerable.Empty<ExceptionInfo>();

        return GetExceptionTypesFromDocumentationCommentXml(compilation, xElement);
    }

    private static XElement? GetDocCommentXml(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
    {
        // Find the syntax node for the method
        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return null;

        var syntaxNode = syntaxRef.GetSyntax(cancellationToken);
        if (syntaxNode is not (MethodDeclarationSyntax or LocalFunctionStatementSyntax))
            return null;

        // Collect documentation trivia
        var trivia = syntaxNode.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                        t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .ToList();

        if (trivia.Count == 0)
            return null;

        var xmlText = string.Concat(trivia.Select(t => t.ToFullString().Replace("///", string.Empty).Trim()));

        try
        {
            // Wrap in a root element in case the comment isn't standalone XML
            return XElement.Parse("<root>" + xmlText + "</root>");
        }
        catch
        {
            return null;
        }
    }
}