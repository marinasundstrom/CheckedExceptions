using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ThrowsAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ThrowStatementAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "THROW002";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Unhandled exception thrown",
        "Exception '{0}' is thrown but not handled or declared via ThrowsAttribute",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Configure analysis settings
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register action for throw statements
        context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
    }

    private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;

        // Ignore re-throw statements (throw;)
        if (throwStatement.Expression == null)
            return;

        // Get the exception type being thrown
        var exceptionType = context.SemanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;
        if (exceptionType == null)
            return;

        // Check if exception is handled in try-catch blocks
        var isHandled = IsExceptionHandledInTryCatch(context, throwStatement, exceptionType);

        // Check if exception is declared via ThrowsAttribute
        var isDeclared = IsExceptionDeclaredInMethodOrConstruct(context, throwStatement, exceptionType);

        if (!isHandled && !isDeclared)
        {
            // Report diagnostic
            var diagnostic = Diagnostic.Create(Rule, throwStatement.GetLocation(), exceptionType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool IsExceptionHandledInTryCatch(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        // Traverse up the syntax tree to find enclosing try-catch blocks
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is TryStatementSyntax tryStatement)
            {
                foreach (var catchClause in tryStatement.Catches)
                {
                    // Handle catch without exception type (catch all)
                    if (catchClause.Declaration == null)
                    {
                        // Catching all exceptions
                        return true;
                    }

                    var catchType = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                    if (catchType != null && exceptionType.IsOrInheritsFrom2(catchType))
                    {
                        // Exception is handled
                        return true;
                    }
                }
            }

            // Continue traversing up the tree
        }

        // Exception is not handled
        return false;
    }

    private bool IsExceptionDeclaredInMethodOrConstruct(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        // Traverse up the syntax tree to find the containing method, property accessor, lambda, or local function
        foreach (var ancestor in node.Ancestors())
        {
            IMethodSymbol methodSymbol = null;

            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            }
            else if (ancestor is AccessorDeclarationSyntax accessorDeclaration)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(accessorDeclaration);
            }
            else if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(localFunction);
            }
            else if (ancestor is AnonymousFunctionExpressionSyntax anonymousFunction)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(anonymousFunction);
                methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            }

            if (methodSymbol != null)
            {
                // Check if methodSymbol has ThrowsAttribute for the exception
                var throwsAttributes = methodSymbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute")
                    .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
                    .Where(exType => exType != null);

                if (throwsAttributes.Any(declaredException => exceptionType.IsOrInheritsFrom2(declaredException)))
                {
                    return true; // Exception is declared via ThrowsAttribute
                }
                else
                {
                    return false; // Exception is not declared
                }
            }
        }

        // Exception is not declared in any containing method or construct
        return false;
    }
}
