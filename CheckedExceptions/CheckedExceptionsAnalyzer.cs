namespace CheckedExceptions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CheckedExceptionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "THROW001";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

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

        // Register action for event assignments (+= and -=)
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.AddAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.SubtractAssignmentExpression);
    }

    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the symbol of the invoked method
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null)
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
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null)
            return;

        AnalyzeCalledMethod(context, objectCreation, methodSymbol);
    }

    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Get the symbol of the member
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        var symbol = symbolInfo.Symbol;

        if (symbol == null)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
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
        // Do not analyze methods here to prevent duplicate diagnostics
    }

    private void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Get the symbol of the indexer
        var symbolInfo = context.SemanticModel.GetSymbolInfo(elementAccess);
        var propertySymbol = symbolInfo.Symbol as IPropertySymbol;

        if (propertySymbol == null)
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

    private void AnalyzeEventAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Check if the left side is an event
        var symbolInfo = context.SemanticModel.GetSymbolInfo(assignment.Left);
        var eventSymbol = symbolInfo.Symbol as IEventSymbol;

        if (eventSymbol == null)
            return;

        // Get the method symbol for the add or remove accessor
        IMethodSymbol methodSymbol = null;

        if (assignment.IsKind(SyntaxKind.AddAssignmentExpression) && eventSymbol.AddMethod != null)
        {
            methodSymbol = eventSymbol.AddMethod;
        }
        else if (assignment.IsKind(SyntaxKind.SubtractAssignmentExpression) && eventSymbol.RemoveMethod != null)
        {
            methodSymbol = eventSymbol.RemoveMethod;
        }

        if (methodSymbol != null)
        {
            AnalyzeCalledMethod(context, assignment, methodSymbol);
        }
    }

    private void AnalyzeCalledMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol)
    {
        if (methodSymbol == null)
            return;

        // Get exceptions from ThrowsAttribute
        var exceptionTypes = GetExceptionTypesFromThrowsAttribute(methodSymbol).ToList();

        // Get exceptions from XML documentation
        var xmlExceptionTypes = GetExceptionTypesFromDocumentation(methodSymbol);
        exceptionTypes.AddRange(xmlExceptionTypes);

        if (!exceptionTypes.Any())
            return;

        // Generate method description
        string methodDescription = GetMethodDescription(methodSymbol);

        // Check if exceptions are handled or declared
        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            if (exceptionType == null)
                continue;

            var isHandled = IsExceptionHandledInTryCatch(context, node, exceptionType);
            var isDeclared = IsExceptionDeclaredInMethod(context, node, exceptionType);

            if (!isHandled && !isDeclared)
            {
                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), methodDescription, exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private IEnumerable<INamedTypeSymbol> GetExceptionTypesFromThrowsAttribute(IMethodSymbol methodSymbol)
    {
        var throwsAttributes = methodSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute");

        foreach (var attr in throwsAttributes)
        {
            if (attr.ConstructorArguments.Length > 0)
            {
                var exceptionType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                if (exceptionType != null)
                {
                    yield return exceptionType;
                }
            }
        }
    }

    private IEnumerable<INamedTypeSymbol> GetExceptionTypesFromDocumentation(IMethodSymbol methodSymbol)
    {
        var xmlDocumentation = methodSymbol.GetDocumentationCommentXml(expandIncludes: true, cancellationToken: default);
        if (string.IsNullOrWhiteSpace(xmlDocumentation))
            yield break;

        var xml = XDocument.Parse(xmlDocumentation);

        // Get all <exception> tags
        var exceptionElements = xml.Descendants("exception");

        foreach (var exceptionElement in exceptionElements)
        {
            var crefAttribute = exceptionElement.Attribute("cref");
            if (crefAttribute != null)
            {
                var crefValue = crefAttribute.Value;

                // Remove 'T:' prefix if present
                if (crefValue.StartsWith("T:"))
                    crefValue = crefValue.Substring(2);

                var exceptionType = methodSymbol.ContainingAssembly.GetTypeByMetadataName(crefValue) ??
                                    methodSymbol.ContainingAssembly.GetTypeByMetadataName(crefValue.Split('.').Last());

                if (exceptionType != null)
                {
                    yield return exceptionType;
                }
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
                    if (propertySymbol.IsIndexer)
                    {
                        // Include parameter types in the indexer description
                        string parameters = string.Join(", ", propertySymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        return $"Indexer '{methodSymbol.ContainingType.Name}[{parameters}]' {accessorType}";
                    }
                    else
                    {
                        return $"Property '{propertySymbol.Name}' {accessorType}";
                    }
                }
                break;

            case MethodKind.EventAdd:
            case MethodKind.EventRemove:
                var eventSymbol = methodSymbol.AssociatedSymbol as IEventSymbol;
                if (eventSymbol != null)
                {
                    string accessorType = methodSymbol.MethodKind == MethodKind.EventAdd ? "adder" : "remover";
                    return $"Event '{eventSymbol.Name}' {accessorType}";
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
        // Traverse up through methods, constructors, property accessors, local functions, and anonymous functions
        foreach (var ancestor in node.Ancestors())
        {
            IMethodSymbol methodSymbol = null;

            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            }
            else if (ancestor is ConstructorDeclarationSyntax constructorDeclaration)
            {
                methodSymbol = context.SemanticModel.GetDeclaredSymbol(constructorDeclaration);
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
                methodSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
            }

            if (methodSymbol != null)
            {
                if (IsExceptionDeclaredInSymbol(methodSymbol, exceptionType))
                    return true;
            }
        }

        // Exception is not declared in any enclosing method, constructor, property accessor, local function, or anonymous function
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
        var parent = expression.Parent;

        if (parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.Left == expression)
                return false; // It's a setter
        }
        else if (parent is PrefixUnaryExpressionSyntax prefixUnary)
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression) || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
                return false; // It's a setter
        }
        else if (parent is PostfixUnaryExpressionSyntax postfixUnary)
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression) || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
                return false; // It's a setter
        }

        return true; // Assume getter in other cases
    }

    private bool IsPropertySetter(ExpressionSyntax expression)
    {
        var parent = expression.Parent;

        if (parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.Left == expression)
                return true; // It's a setter
        }
        else if (parent is PrefixUnaryExpressionSyntax prefixUnary)
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression) || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
                return true; // It's a setter
        }
        else if (parent is PostfixUnaryExpressionSyntax postfixUnary)
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression) || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
                return true; // It's a setter
        }

        return false; // Assume getter in other cases
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