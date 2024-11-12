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

        // Register action for method invocations
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

        AnalyzeCalledMethod(context, invocation, methodSymbol);
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
                // For constructors, display "Constructor 'ClassName'"
                return $"Constructor '{methodSymbol.ContainingType.Name}'";

            case MethodKind.PropertyGet:
            case MethodKind.PropertySet:
                // For property accessors, display "Property 'PropertyName' getter/setter"
                var propertySymbol = methodSymbol.AssociatedSymbol as IPropertySymbol;
                if (propertySymbol != null)
                {
                    string accessorType = methodSymbol.MethodKind == MethodKind.PropertyGet ? "getter" : "setter";
                    return $"Property '{propertySymbol.Name}' {accessorType}";
                }
                break;

            default:
                // For regular methods, display "Method 'MethodName'"
                return $"Method '{methodSymbol.Name}'";
        }

        // Fallback to method name if none of the above
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

    private bool IsExceptionDeclaredInMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        // Get the containing method
        var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
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