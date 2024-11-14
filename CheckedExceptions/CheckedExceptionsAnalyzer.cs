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
    public const string DiagnosticId2 = "THROW002";
    public const string DiagnosticId3 = "THROW003";
    public const string DiagnosticId4 = "THROW004";

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

    private static readonly DiagnosticDescriptor Rule2 = new DiagnosticDescriptor(
        DiagnosticId2,
        "Unhandled exception thrown",
        "Exception '{0}' is thrown but not handled or declared via ThrowsAttribute",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Rule3 = new DiagnosticDescriptor(
        DiagnosticId3,
        "Avoid declaring Throws(typeof(Exception))",
        "Declaring Throws(typeof(Exception)) is too general; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Rule4 = new DiagnosticDescriptor(
        DiagnosticId4,
        "Avoid throwing general Exception",
        "Throwing 'Exception' is too general; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule, Rule2, Rule3, Rule4);

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

        // Register action for throw statements
        context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);

        // Register action for throw expressions
        context.RegisterSyntaxNodeAction(AnalyzeThrowExpression, SyntaxKind.ThrowExpression);

        // Register action for analyzing method attributes
        context.RegisterSyntaxNodeAction(AnalyzeMethodAttributes, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethodAttributes(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // Retrieve the method symbol
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null)
            return;

        // Find all [Throws] attributes on the method
        var throwsAttributes = methodSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute");

        foreach (var attribute in throwsAttributes)
        {
            // Check if the attribute argument is Exception
            var exceptionType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (exceptionType?.Name == "Exception")
            {
                // Get the syntax node directly from the attribute's ApplicationSyntaxReference
                var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;

                if (attributeSyntax != null)
                {
                    // Report diagnostic on [Throws(typeof(Exception))] location
                    var diagnostic = Diagnostic.Create(Rule3, attributeSyntax.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private void AnalyzeThrowExpression(SyntaxNodeAnalysisContext context)
    {
        var throwExpression = (ThrowExpressionSyntax)context.Node;

        // Get the exception type being thrown
        if (throwExpression.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            if (exceptionType == null)
                return;

            // Report diagnostic if throwing "Exception" directly (as before)
            if (exceptionType.Name == "Exception")
            {
                var diagnostic = Diagnostic.Create(Rule4, throwExpression.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            // Check if the exception is handled in a try-catch block
            var isHandled = IsExceptionHandledInTryCatch(context, throwExpression, exceptionType);

            // Check if the exception is declared with [Throws] in the containing method
            var isDeclared = IsExceptionDeclaredInMethod(context, throwExpression, exceptionType);

            // Report diagnostic if the exception is neither handled nor declared
            if (!isHandled && !isDeclared)
            {
                var diagnostic = Diagnostic.Create(Rule2, throwExpression.GetLocation(), exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;

        // Check for throw new Exception()
        if (throwStatement.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            if (exceptionType != null && exceptionType.Name == "Exception")
            {
                var diagnostic = Diagnostic.Create(Rule4, throwStatement.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Check if this is a rethrow (throw;) within a catch block
        if (throwStatement.Expression == null && IsWithinCatchBlock(throwStatement))
        {
            // Find the exception type being rethrown by analyzing the catch block
            var exceptionType = GetExceptionTypeFromCatchBlock(context, throwStatement);
            if (exceptionType == null)
                return;

            // Check if exception is declared via ThrowsAttribute
            var isDeclared = IsExceptionDeclaredInMethod(context, throwStatement, exceptionType);

            if (!isDeclared)
            {
                // Issue a diagnostic if the rethrown exception is not declared
                var diagnostic = Diagnostic.Create(Rule2, throwStatement.GetLocation(), exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
            }
            return; // No further analysis for rethrows
        }

        // For regular throw statements (not rethrows), analyze normally
        var thrownExceptionType = context.SemanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;
        if (thrownExceptionType == null)
            return;

        var isHandled = IsExceptionHandledInTryCatch(context, throwStatement, thrownExceptionType);
        var isDeclaredRegular = IsExceptionDeclaredInMethod(context, throwStatement, thrownExceptionType);

        if (!isHandled && !isDeclaredRegular)
        {
            var diagnostic = Diagnostic.Create(Rule2, throwStatement.GetLocation(), thrownExceptionType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void CheckForGeneralThrows(SyntaxNodeAnalysisContext context, IMethodSymbol methodSymbol, AttributeSyntax throwsAttribute)
    {
        var exceptionType = throwsAttribute.ArgumentList.Arguments[0].Expression;
        var typeInfo = context.SemanticModel.GetTypeInfo(exceptionType).Type as INamedTypeSymbol;

        if (typeInfo != null && typeInfo.Name == "Exception")
        {
            var diagnostic = Diagnostic.Create(Rule3, throwsAttribute.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private INamedTypeSymbol GetExceptionTypeFromCatchBlock(SyntaxNodeAnalysisContext context, ThrowStatementSyntax throwStatement)
    {
        var catchClause = throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();

        if (catchClause == null)
            return null;

        // If no specific exception type is declared, default to Exception
        if (catchClause.Declaration?.Type == null)
        {
            var exceptionTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Exception");
            return exceptionTypeSymbol;
        }

        // Otherwise, return the declared exception type
        return context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
    }

    private bool IsWithinCatchBlock(SyntaxNode node)
    {
        return node.Ancestors().OfType<CatchClauseSyntax>().Any();
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
        var xmlExceptionTypes = GetExceptionTypesFromDocumentation(context.Compilation, methodSymbol);
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

    private IEnumerable<INamedTypeSymbol> GetExceptionTypesFromDocumentation(Compilation compilation, IMethodSymbol methodSymbol)
    {
        var xmlDocumentation = methodSymbol.GetDocumentationCommentXml(expandIncludes: true);

        if (string.IsNullOrWhiteSpace(xmlDocumentation)) return Enumerable.Empty<INamedTypeSymbol>();

        XDocument xml;
        try
        {
            xml = XDocument.Parse(xmlDocumentation);
        }
        catch (Exception ex)
        {
            // Handle or log the parsing error
            return Enumerable.Empty<INamedTypeSymbol>();
        }

        List<INamedTypeSymbol> exceptionTypes = new List<INamedTypeSymbol>();

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

                var exceptionType = compilation.GetTypeByMetadataName(crefValue) ??
                                    compilation.GetTypeByMetadataName(crefValue.Split('.').Last());

                if (exceptionType != null)
                {
                    exceptionTypes.Add(exceptionType);
                }
            }
        }

        return exceptionTypes;
    }

    private string GetMethodDescription(IMethodSymbol methodSymbol)
    {
        switch (methodSymbol.MethodKind)
        {
            case MethodKind.Constructor:
                string constructorParameters = string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                return $"Constructor '{methodSymbol.ContainingType.Name}({constructorParameters})'";

            case MethodKind.PropertyGet:
            case MethodKind.PropertySet:
                var propertySymbol = methodSymbol.AssociatedSymbol as IPropertySymbol;
                if (propertySymbol != null)
                {
                    string accessorType = methodSymbol.MethodKind == MethodKind.PropertyGet ? "getter" : "setter";
                    if (propertySymbol.IsIndexer)
                    {
                        // Include parameter types in the indexer description
                        string propertyParameters = string.Join(", ", propertySymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        return $"Indexer '{methodSymbol.ContainingType.Name}[{propertyParameters}]' {accessorType}";
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
                string localFunctionParameters = string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                return $"Local function '{methodSymbol.Name}({localFunctionParameters})'";

            case MethodKind.AnonymousFunction:
                return $"Lambda expression";

            default:
                string methodParameters = string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                return $"Method '{methodSymbol.Name}({methodParameters})'";
        }

        string methodParameters2 = string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        return $"Method '{methodSymbol.Name}({methodParameters2})'";
    }

    private bool IsExceptionHandledInTryCatch(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is TryStatementSyntax tryStatement)
            {
                foreach (var catchClause in tryStatement.Catches)
                {
                    // Handle catch without exception type (catch all)
                    if (catchClause.Declaration == null)
                    {
                        return true;
                    }

                    var catchType = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                    if (catchType != null && exceptionType.IsOrInheritsFrom(catchType))
                    {
                        return true; // Exception is handled
                    }
                }
            }
        }
        return false;
    }

    private bool IsNodeInCatchBlock(SyntaxNode node, TryStatementSyntax tryStatement)
    {
        foreach (var catchClause in tryStatement.Catches)
        {
            if (catchClause.Block.Contains(node))
            {
                return true;
            }
        }
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