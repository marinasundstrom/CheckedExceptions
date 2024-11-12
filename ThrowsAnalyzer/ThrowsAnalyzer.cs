namespace ThrowsAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ThrowsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "THROW001",
        "Unhandled exception",
        "Method '{0}' throws exception '{1}' which is not handled",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Configure analysis settings
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register action for invocation expressions
        context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the symbol of the invoked method
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Get the ThrowsAttribute applied to the method
        var throwsAttributes = methodSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute")
            .ToArray();

        if (!throwsAttributes.Any())
            return;

        // Get the exception types specified in ThrowsAttribute
        var exceptionTypes = throwsAttributes
            .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
            .Where(exType => exType != null)
            .ToArray();

        // Check if exceptions are handled or declared
        foreach (var exceptionType in exceptionTypes)
        {
            var isHandled = IsExceptionHandledInTryCatch(context, invocation, exceptionType);
            var isDeclared = IsExceptionDeclaredInMethod(context, invocation, exceptionType);

            if (!isHandled && !isDeclared)
            {
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodSymbol.Name, exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private bool IsExceptionHandledInTryCatch(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol exceptionType)
    {
        // Traverse up the syntax tree to find enclosing try-catch blocks
        foreach (var ancestor in invocation.Ancestors())
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
                    if (catchType != null && exceptionType.IsOrInheritsFrom(catchType))
                    {
                        // Exception is handled
                        return true;
                    }
                }
            }

            // Stop traversal if we reach an outer method or lambda expression
            if (ancestor is MethodDeclarationSyntax || ancestor is AnonymousFunctionExpressionSyntax)
            {
                // Do not break here; continue traversal to check for outer try-catch blocks
                // Optionally, you can break if you want to limit the scope to the current method
            }
        }

        // Exception is not handled
        return false;
    }

    private bool IsExceptionDeclaredInMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol exceptionType)
    {
        // Get the containing method
        var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod != null)
        {
            var containingMethodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
            if (containingMethodSymbol != null)
            {
                var callerThrowsAttributes = containingMethodSymbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute")
                    .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
                    .Where(exType => exType != null)
                    .ToArray();

                return callerThrowsAttributes.Any(callerExType =>
                    exceptionType.IsOrInheritsFrom(callerExType));
            }
        }

        return false;
    }
}

public static class TypeSymbolExtensions
{
    // Extension method to check if 'type' inherits from or is equal to 'baseType'
    public static bool IsOrInheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (type == null || baseType == null)
            return false;

        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;

            current = current.BaseType;
        }
        return false;
    }
}