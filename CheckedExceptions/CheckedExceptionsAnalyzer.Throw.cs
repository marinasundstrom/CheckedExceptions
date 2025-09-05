using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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

        var classification = GetExceptionClassification(exceptionType, settings);

        if (classification is ExceptionClassification.Ignored)
        {
            return;
        }
        else if (classification is ExceptionClassification.Informational)
        {
            var diagnostic = Diagnostic.Create(RuleIgnoredException, GetSignificantLocation(node), exceptionType.Name);
            reportDiagnostic(diagnostic);
            return;
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
            if (settings.IsLinqImplicitlyDeclaredExceptionsEnabled
                && IsInsideLinqLambda(node, semanticModel, settings, out _))
            {
                // Implicitly declared exceptions in LINQ expressions

                var properties = ImmutableDictionary.Create<string, string?>()
                    .Add("ExceptionType", exceptionType.Name);

                var diagnostic = Diagnostic.Create(
                    RuleImplicitlyDeclaredException,
                    GetSignificantLocation(node),
                    properties,
                    exceptionType.Name);

                reportDiagnostic(diagnostic);
                return;
            }
            else
            {
                // Normal diagnostics

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

    private static bool IsInsideLinqLambda(
        SyntaxNode node,
        SemanticModel semanticModel,
        AnalyzerSettings settings,
        out IInvocationOperation? linqInvocation,
        CancellationToken ct = default)
    {
        linqInvocation = null;

        // Start from the operation that corresponds to the node
        var op = semanticModel.GetOperation(node, ct);
        if (op is null) return false;

        // Walk up to find an enclosing anonymous function (lambda/local-func-as-anon)
        IAnonymousFunctionOperation? lambda = null;
        for (var cur = op; cur is not null; cur = cur.Parent)
        {
            if (cur is IAnonymousFunctionOperation anon)
            {
                lambda = anon;
                break;
            }
        }
        if (lambda is null) return false;

        // From the lambda, climb to its enclosing invocation:
        // Lambda -> (optional) IDelegateCreation/Conversion -> IArgument -> IInvocation
        IOperation? p = lambda.Parent;

        // unwrap delegate creation/conversions
        while (p is IDelegateCreationOperation || p is IConversionOperation || p is IParenthesizedOperation)
            p = p.Parent;

        // the lambda should be inside an argument
        if (p is not IArgumentOperation arg) return false;

        // then the argument should belong to an invocation
        var inv = arg.Parent as IInvocationOperation;
        if (inv is null) return false;

        // finally: is it a LINQ query operator?
        if (!IsLinqExtension(inv.TargetMethod, settings)) return false;

        linqInvocation = inv;
        return true;
    }
}