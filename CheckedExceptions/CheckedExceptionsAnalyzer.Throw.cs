using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void AnalyzeExceptionThrowingNode(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        INamedTypeSymbol? exceptionType,
        AnalyzerSettings settings,
        IList<INamedTypeSymbol>? unhandledExceptions = null)
    {
        AnalyzeExceptionThrowingNode(
            context.SemanticModel,
            context.ReportDiagnostic,
            node,
            exceptionType,
            settings,
            unhandledExceptions);
    }

    /// <summary>
    /// Analyzes a node that throws or propagates exceptions to check for handling or declaration.
    /// </summary>
    private static void AnalyzeExceptionThrowingNode(
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic,
        SyntaxNode node,
        INamedTypeSymbol? exceptionType,
        AnalyzerSettings settings,
        IList<INamedTypeSymbol>? unhandledExceptions = null)
    {
        if (exceptionType is null)
            return;

        var exceptionName = exceptionType.ToDisplayString();

        if (FilterIgnored(settings, exceptionName))
        {
            // Completely ignore this exception
            return;
        }
        else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
        {
            if (ShouldIgnore(node, mode))
            {
                // Report as THROW002 (Info level)
                var diagnostic = Diagnostic.Create(RuleIgnoredException, GetSignificantLocation(node), exceptionType.Name);
                reportDiagnostic(diagnostic);
                return;
            }
        }

        if (settings.BaseExceptionThrownDiagnosticEnabled)
        {
            // Check for general exceptions
            if (node is not InvocationExpressionSyntax && exceptionType.IsGeneralException())
            {
                reportDiagnostic(Diagnostic.Create(RuleGeneralThrow, GetSignificantLocation(node)));
            }
        }

        // Check if the exception is declared via [Throws]
        var isDeclared = IsExceptionDeclaredInMember(semanticModel, node, exceptionType);

        // Determine if the exception is handled by any enclosing try-catch
        var isHandled = IsExceptionHandledByEnclosingTryCatch(node, exceptionType, semanticModel);

        // Report diagnostic if neither handled nor declared
        if (!isHandled && !isDeclared)
        {
            var properties = ImmutableDictionary.Create<string, string?>()
                .Add("ExceptionType", exceptionType.Name);

            var diagnostic = Diagnostic.Create(
                RuleUnhandledException,
                GetSignificantLocation(node),
                properties,
                exceptionType.Name);

            reportDiagnostic(diagnostic);

            // 🔑 Collect for later redundancy analysis
            unhandledExceptions?.Add(exceptionType);
        }
    }
}