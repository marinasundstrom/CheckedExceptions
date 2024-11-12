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
        "{0} throws exception '{1}' which is not handled",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Configure analysis settings
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register action for method invocations (includes local function calls and delegate invocations)
        context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);

        // Register action for object creation expressions (constructors)
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);

        // Register action for member access expressions (properties)
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

        // Register action for element access expressions (indexers)
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }

    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the symbol of the invoked method
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Handle delegate invokes by getting the target method symbol
        if (methodSymbol.MethodKind == MethodKind.DelegateInvoke)
        {
            var targetMethodSymbol = GetTargetMethodSymbol(context, invocation);
            if (targetMethodSymbol != null)
            {
                methodSymbol = targetMethodSymbol;
            }
            else
            {
                // Could not find the target method symbol
                return;
            }
        }

        AnalyzeCalledMethod(context, invocation, methodSymbol);
    }

    private IMethodSymbol GetTargetMethodSymbol(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression;

        // Get the symbol of the expression being invoked
        var symbolInfo = context.SemanticModel.GetSymbolInfo(expression);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (symbol == null)
            return null;

        if (symbol is ILocalSymbol localSymbol)
        {
            // Get the syntax node where the local variable is declared
            var declaringSyntaxReference = localSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (declaringSyntaxReference != null)
            {
                var syntaxNode = declaringSyntaxReference.GetSyntax();

                if (syntaxNode is VariableDeclaratorSyntax variableDeclarator)
                {
                    var initializer = variableDeclarator.Initializer?.Value;

                    if (initializer != null)
                    {
                        // Get the method symbol of the initializer (lambda expression or method group)
                        var initializerSymbolInfo = context.SemanticModel.GetSymbolInfo(initializer);
                        var initializerSymbol = initializerSymbolInfo.Symbol ?? initializerSymbolInfo.CandidateSymbols.FirstOrDefault();

                        if (initializerSymbol is IMethodSymbol targetMethodSymbol)
                        {
                            return targetMethodSymbol;
                        }
                    }
                }
            }
        }

        return null;
    }

    private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Get the symbol of the constructor being called
        var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        AnalyzeCalledMethod(context, objectCreation, methodSymbol);
    }

    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Get the symbol of the member
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
            return;

        // Handle getter and setter
        var isGetter = IsPropertyGetter(memberAccess);
        var isSetter = IsPropertySetter(memberAccess);

        if (isGetter && propertySymbol.GetMethod != null)
        {
            AnalyzeCalledMethod(context, memberAccess, propertySymbol.GetMethod);
        }

        if (isSetter && propertySymbol.SetMethod != null)
        {
            AnalyzeCalledMethod(context, memberAccess, propertySymbol.SetMethod);
        }
    }

    private void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Get the symbol of the indexer
        var symbolInfo = context.SemanticModel.GetSymbolInfo(elementAccess);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
            return;

        // Handle getter and setter
        var isGetter = IsPropertyGetter(elementAccess);
        var isSetter = IsPropertySetter(elementAccess);

        if (isGetter && propertySymbol.GetMethod != null)
        {
            AnalyzeCalledMethod(context, elementAccess, propertySymbol.GetMethod);
        }

        if (isSetter && propertySymbol.SetMethod != null)
        {
            AnalyzeCalledMethod(context, elementAccess, propertySymbol.SetMethod);
        }
    }

    private void AnalyzeCalledMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol)
    {
        if (methodSymbol == null)
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

        // Generate method description
        string methodDescription = GetMethodDescription(methodSymbol);

        // Check if exceptions are handled or declared
        foreach (var exceptionType in exceptionTypes)
        {
            var isHandled = IsExceptionHandledInTryCatch(context, node, exceptionType);
            var isDeclared = IsExceptionDeclaredInMethod(context, node, exceptionType);

            if (!isHandled && !isDeclared)
            {
                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), methodDescription, exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private string GetMethodDescription(IMethodSymbol methodSymbol)
    {
        switch (methodSymbol.MethodKind)
        {
            case MethodKind.Constructor:
                return $"Constructor '{methodSymbol.ContainingType.Name}'";

            case MethodKind.PropertyGet:
            case MethodKind.PropertySet:
                var propertySymbol = methodSymbol.AssociatedSymbol as IPropertySymbol;
                if (propertySymbol != null)
                {
                    string accessorType = methodSymbol.MethodKind == MethodKind.PropertyGet ? "getter" : "setter";
                    return $"Property '{propertySymbol.Name}' {accessorType}";
                }
                break;

            case MethodKind.LocalFunction:
                return $"Local function '{methodSymbol.Name}'";

            case MethodKind.AnonymousFunction:
                return $"Lambda expression";

            default:
                return $"Method '{methodSymbol.Name}'";
        }

        return $"Method '{methodSymbol.Name}'";
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
                    if (catchType != null && exceptionType.IsOrInheritsFrom(catchType))
                    {
                        // Exception is handled
                        return true;
                    }
                }
            }

            // Do not break; continue traversal to check for outer try-catch blocks
        }

        // Exception is not handled
        return false;
    }

    private bool IsExceptionDeclaredInMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        // Traverse up through methods, local functions, and anonymous functions
        foreach (var ancestor in node.Ancestors())
        {
            IMethodSymbol methodSymbol = null;

            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            }
            else if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(localFunction);
            }
            else if (ancestor is AnonymousFunctionExpressionSyntax anonymousFunction)
            {
                methodSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
            }

            if (methodSymbol != null)
            {
                if (IsExceptionDeclaredInSymbol(methodSymbol, exceptionType))
                    return true;
            }
        }

        // Exception is not declared in any enclosing method, local function, or anonymous function
        return false;
    }

    private bool IsExceptionDeclaredInSymbol(IMethodSymbol methodSymbol, INamedTypeSymbol exceptionType)
    {
        if (methodSymbol != null)
        {
            var throwsAttributes = methodSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute")
                .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
                .Where(exType => exType != null);

            return throwsAttributes.Any(declaredException =>
                exceptionType.IsOrInheritsFrom(declaredException));
        }
        return false;
    }

    private bool IsPropertyGetter(ExpressionSyntax expression)
    {
        // Check if the expression is being read (RHS of an assignment or in an expression)
        var parent = expression.Parent;
        if (parent is AssignmentExpressionSyntax assignment && assignment.Left == expression)
            return false; // It's a setter

        return true; // Assume getter in other cases
    }

    private bool IsPropertySetter(ExpressionSyntax expression)
    {
        // Check if the expression is being assigned a value
        var parent = expression.Parent;
        if (parent is AssignmentExpressionSyntax assignment && assignment.Left == expression)
            return true; // It's a setter

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

        return type.Equals(baseType, SymbolEqualityComparer.Default) || type.InheritsFrom(baseType);
    }

    public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.Equals(baseType, SymbolEqualityComparer.Default))
                return true;
        }
        return false;
    }
}