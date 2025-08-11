using System.Collections.Immutable;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void CheckXmlDocsForUndeclaredExceptions(
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

    private static void CheckXmlDocsForUndeclaredExceptions_Method(
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

    private static void CheckXmlDocsForUndeclaredExceptions_Property(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var propertySymbol = semanticModel.GetDeclaredSymbol(context.Node) as IPropertySymbol;

        if (propertySymbol is null)
            return;

        var compilation = context.Compilation;

        // Gather existing [Throws] types
        var declaredExceptions = throwsAttributes.Select(throwsAttribute => GetExceptionTypes(throwsAttribute, semanticModel))
            .SelectMany(x => x)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        CheckXmlDocsForUndeclaredExceptionsCore(declaredExceptions, propertySymbol!, compilation, context.ReportDiagnostic);
    }

    private static void CheckXmlDocsForUndeclaredExceptions_ExpressionBodiedProperty(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var propertySymbol = semanticModel.GetDeclaredSymbol(context.Node) as IPropertySymbol;

        if (propertySymbol is null)
            return;

        var compilation = context.Compilation;

        // Gather existing [Throws] types
        var declaredExceptions = throwsAttributes.Select(throwsAttribute => GetExceptionTypes(throwsAttribute, semanticModel))
            .SelectMany(x => x)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        if (propertySymbol.GetMethod is null)
            return;

        CheckXmlDocsForUndeclaredExceptionsCore(declaredExceptions, propertySymbol!, compilation, context.ReportDiagnostic);
    }

    private static void CheckXmlDocsForUndeclaredExceptionsCore(IEnumerable<ISymbol?> exceptionTypes, ISymbol symbol, Compilation compilation, Action<Diagnostic> reportDiagnostic)
    {
        // Parse <exception cref="..."/>
        var xmlDocumentedExceptions = GetExceptionTypesFromDocumentationCommentXml_Syntax(compilation, symbol).ToList();

        if (xmlDocumentedExceptions.Count() == 0)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
            // Expression-bodied property? (no accessor list, but has an expression body)
            if (propertySymbol.DeclaringSyntaxReferences
                              .Select(r => r.GetSyntax())
                              .OfType<PropertyDeclarationSyntax>()
                              .Any(p => p.ExpressionBody is not null))
            {
                // ✅ Anchor diagnostics on the property itself
                var exTypes = GetExceptionTypes(propertySymbol);
                ProcessDiagnostics(exTypes, propertySymbol, reportDiagnostic, xmlDocumentedExceptions);
                return;
            }

            // Filter exceptions documented specifically for the getter and setter
            var getterExceptions = xmlDocumentedExceptions.Where(x => HeuristicRules.IsForGetter(x.Description));

            var setterExceptions = xmlDocumentedExceptions.Where(x => HeuristicRules.IsForSetter(x.Description));

            var allOtherExceptions = xmlDocumentedExceptions
                .Except(getterExceptions);
            allOtherExceptions = allOtherExceptions
                .Except(setterExceptions);

            if (propertySymbol.GetMethod is not null)
            {
                var exTypes = GetExceptionTypes(propertySymbol.GetMethod);

                foreach (var getterException in getterExceptions)
                {
                    ProcessDiagnostics(exTypes, propertySymbol.GetMethod!, reportDiagnostic, getterExceptions);
                }

                foreach (var exception in allOtherExceptions)
                {
                    ProcessDiagnostics(exTypes, propertySymbol.GetMethod!, reportDiagnostic, allOtherExceptions);
                }
            }

            if (propertySymbol.SetMethod is not null)
            {
                var exTypes = GetExceptionTypes(propertySymbol.SetMethod);

                foreach (var setterException in setterExceptions)
                {
                    ProcessDiagnostics(exTypes, propertySymbol.SetMethod!, reportDiagnostic, setterExceptions);
                }

                if (propertySymbol.GetMethod is null)
                {
                    foreach (var exception in allOtherExceptions)
                    {
                        ProcessDiagnostics(exTypes, propertySymbol.SetMethod!, reportDiagnostic, allOtherExceptions);
                    }
                }
            }
        }
        else
        {
            // Method 

            ProcessDiagnostics(exceptionTypes, symbol, reportDiagnostic, xmlDocumentedExceptions);
        }
    }

    private static void ProcessDiagnostics(IEnumerable<ISymbol?> throwsAttributes, ISymbol symbol, Action<Diagnostic> reportDiagnostic, IEnumerable<ExceptionInfo> xmlDocExceptions)
    {
        // Find differences: XML-declared but not in [Throws]
        foreach (var exceptionInfo in xmlDocExceptions)
        {
            if (exceptionInfo.ExceptionType is null)
                continue;

            // Handle inheritance?
            if (!throwsAttributes.Contains(exceptionInfo.ExceptionType, SymbolEqualityComparer.Default))
            {
                var properties = ImmutableDictionary.Create<string, string?>()
                    .Add("ExceptionType", exceptionInfo.ExceptionType.Name);

                var diag = Diagnostic.Create(
                    RuleXmlDocButNoThrows,
                    symbol.Locations.FirstOrDefault() ?? Location.None,
                    properties,
                    exceptionInfo.ExceptionType.Name);

                reportDiagnostic(diag);
            }
        }
    }

    private static IEnumerable<ExceptionInfo> GetExceptionTypesFromDocumentationCommentXml_Syntax(
        Compilation compilation,
        ISymbol symbol,
        CancellationToken cancellationToken = default)
    {
        var xElement = GetDocCommentXml(symbol, cancellationToken);
        if (xElement is null)
            return Enumerable.Empty<ExceptionInfo>();

        return GetExceptionTypesFromDocumentationCommentXml(compilation, xElement);
    }

    private static XElement? GetDocCommentXml(ISymbol symbol, CancellationToken cancellationToken)
    {
        // Find the syntax node for the method
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return null;

        var syntaxNode = syntaxRef.GetSyntax(cancellationToken);
        if (syntaxNode is not (BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or BasePropertyDeclarationSyntax))
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