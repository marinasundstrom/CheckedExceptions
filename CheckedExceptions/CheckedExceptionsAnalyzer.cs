namespace Sundstrom.CheckedExceptions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class CheckedExceptionsAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic IDs
    public const string DiagnosticIdUnhandled = "THROW001";
    public const string DiagnosticIdGeneralThrows = "THROW003";
    public const string DiagnosticIdGeneralThrow = "THROW004";
    public const string DiagnosticIdDuplicateThrow = "THROW005";

    public static IEnumerable<string> AllDiagnosticsIds = [DiagnosticIdUnhandled, DiagnosticIdGeneralThrows, DiagnosticIdGeneralThrow, DiagnosticIdDuplicateThrow];

    private static readonly DiagnosticDescriptor RuleUnhandledException = new(
        DiagnosticIdUnhandled,
        "Unhandled exception thrown",
        "Exception '{0}' is thrown but not handled or declared via ThrowsAttribute",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleGeneralThrow = new(
        DiagnosticIdGeneralThrow,
        "Avoid throwing general Exception",
        "Throwing 'Exception' is too general; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleGeneralThrows = new DiagnosticDescriptor(
        DiagnosticIdGeneralThrows,
        "Avoid declaring Throws(typeof(Exception))",
        "Declaring Throws(typeof(Exception)) is too general; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleDuplicateThrow = new DiagnosticDescriptor(
        DiagnosticIdDuplicateThrow,
        "Avoid duplicate ThrowsAttributes declaring the same exception type",
        "Multiple ThrowsAttributes declare the same exception type '{0}'. Remove the duplicates to avoid redundancy.",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RuleUnhandledException, RuleGeneralThrows, RuleGeneralThrow, RuleDuplicateThrow];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register actions for throw statements and expressions
        context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
        context.RegisterSyntaxNodeAction(AnalyzeThrowExpression, SyntaxKind.ThrowExpression);

        context.RegisterSymbolAction(AnalyzeMethodSymbol, SymbolKind.Method);

        context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunctionStatement, SyntaxKind.LocalFunctionStatement);

        // Register additional actions for method calls, object creations, etc.
        context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.AddAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.SubtractAssignmentExpression);
    }

    private void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
    {
        var lambdaExpression = (LambdaExpressionSyntax)context.Node;
        AnalyzeFunctionAttributes(lambdaExpression, lambdaExpression.AttributeLists.SelectMany(a => a.Attributes), context.SemanticModel, context);
    }

    private void AnalyzeLocalFunctionStatement(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        AnalyzeFunctionAttributes(localFunction, localFunction.AttributeLists.SelectMany(a => a.Attributes), context.SemanticModel, context);
    }

    private void AnalyzeFunctionAttributes(SyntaxNode node, IEnumerable<AttributeSyntax> attributes, SemanticModel semanticModel, SyntaxNodeAnalysisContext context)
    {
        var throwsAttributes = attributes
            .Where(attr => IsThrowsAttribute(attr, semanticModel))
            .ToList();

        if (throwsAttributes.Count == 0)
            return;

        CheckForGeneralExceptionThrows(context, throwsAttributes);

        if (throwsAttributes.Count > 1)
        {
            CheckForDuplicateThrowsAttributes(throwsAttributes, context);
        }
    }

    /// <summary>
    /// Determines whether the given attribute is a ThrowsAttribute.
    /// </summary>
    private bool IsThrowsAttribute(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        var attributeSymbol = semanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
        if (attributeSymbol == null)
            return false;

        var attributeType = attributeSymbol.ContainingType;
        return attributeType.Name == "ThrowsAttribute";
    }

    private void AnalyzeMethodSymbol(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol is null)
            return;

        var throwsAttributes = GetThrowsAttributes(methodSymbol).ToImmutableArray();

        if (throwsAttributes.Count() == 0)
            return;

        CheckForGeneralExceptionThrows(throwsAttributes, context);

        if (throwsAttributes.Count() > 1)
        {
            CheckForDuplicateThrowsAttributes(context, throwsAttributes);
        }
    }

    private static IEnumerable<AttributeData> FilterThrowsAttributesByException(ImmutableArray<AttributeData> exceptionAttributes, string exceptionTypeName)
    {
        return exceptionAttributes
            .Where(attribute => IsThrowsAttributeForException(attribute, exceptionTypeName));
    }

    public static bool IsThrowsAttributeForException(AttributeData attribute, string exceptionTypeName)
    {
        if (!attribute.ConstructorArguments.Any())
            return false;

        var exceptionType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        return exceptionType?.Name == exceptionTypeName;
    }

    /// <summary>
    /// Analyzes throw statements to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;

        // Handle rethrows (throw;)
        if (throwStatement.Expression == null)
        {
            if (IsWithinCatchBlock(throwStatement, out var catchClause))
            {
                if (catchClause != null)
                {
                    if (catchClause.Declaration == null)
                    {
                        // General catch block with 'throw;'
                        // Analyze exceptions thrown in the try block
                        var tryStatement = catchClause.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault();
                        if (tryStatement != null)
                        {
                            AnalyzeExceptionsInTryBlock(context, tryStatement, catchClause, throwStatement);
                        }
                    }
                    else
                    {
                        // Specific catch block with 'throw;'
                        // The exception is considered handled
                        // No need to report as unhandled
                    }
                }
            }
            return; // No further analysis for rethrows
        }

        // Handle throw new ExceptionType()
        if (throwStatement.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            AnalyzeExceptionThrowingNode(context, throwStatement, exceptionType);
        }
    }

    private void AnalyzeExceptionsInTryBlock(SyntaxNodeAnalysisContext context, TryStatementSyntax tryStatement, CatchClauseSyntax generalCatchClause, ThrowStatementSyntax throwStatement)
    {
        var semanticModel = context.SemanticModel;

        // Collect exceptions that can be thrown in the try block
        var thrownExceptions = CollectUnhandledExceptions(tryStatement.Block, semanticModel);

        // Collect exception types handled by preceding catch clauses
        var handledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var catchClause in tryStatement.Catches)
        {
            if (catchClause == generalCatchClause)
                break; // Stop at the general catch clause

            if (catchClause.Declaration != null)
            {
                var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                if (catchType != null)
                {
                    handledExceptions.Add(catchType);
                }
            }
            else
            {
                // General catch clause before our general catch; handles all exceptions
                handledExceptions = null;
                break;
            }
        }

        if (handledExceptions == null)
        {
            // All exceptions are handled by a previous general catch
            return;
        }

        // For each thrown exception, check if it is handled
        foreach (var exceptionType in thrownExceptions)
        {
            bool isHandled = handledExceptions.Any(handledException =>
                exceptionType.Equals(handledException, SymbolEqualityComparer.Default) ||
                exceptionType.InheritsFrom(handledException));

            bool isDeclared = IsExceptionDeclaredInMethod(context, tryStatement, exceptionType);

            if (!isHandled && !isDeclared)
            {
                // Report diagnostic for unhandled exception
                var diagnostic = Diagnostic.Create(
                    RuleUnhandledException,
                    throwStatement.GetLocation(),
                    exceptionType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private HashSet<INamedTypeSymbol> CollectUnhandledExceptions(BlockSyntax block, SemanticModel semanticModel)
    {
        var unhandledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (statement is TryStatementSyntax tryStatement)
            {
                // Recursively collect exceptions from the inner try block
                var innerUnhandledExceptions = CollectUnhandledExceptions(tryStatement.Block, semanticModel);

                // Remove exceptions that are caught by the inner catch clauses
                var caughtExceptions = GetCaughtExceptions(tryStatement.Catches, semanticModel);
                innerUnhandledExceptions.RemoveWhere(exceptionType =>
                    IsExceptionCaught(exceptionType, caughtExceptions));

                // Add any exceptions that are not handled in the inner try block
                unhandledExceptions.UnionWith(innerUnhandledExceptions);
            }
            else
            {
                // Collect exceptions thrown in this statement
                var statementExceptions = CollectExceptionsFromStatement(statement, semanticModel);

                // Add them to the unhandled exceptions
                unhandledExceptions.UnionWith(statementExceptions);
            }
        }

        return unhandledExceptions;
    }

    private HashSet<INamedTypeSymbol> CollectExceptionsFromStatement(StatementSyntax statement, SemanticModel semanticModel)
    {
        var exceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Collect exceptions from throw statements
        var throwStatements = statement.DescendantNodesAndSelf().OfType<ThrowStatementSyntax>();
        foreach (var throwStatement in throwStatements)
        {
            if (throwStatement.Expression != null)
            {
                var exceptionType = semanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;
                if (exceptionType != null)
                {
                    exceptions.Add(exceptionType);
                }
            }
        }

        // Collect exceptions from method calls and other expressions
        var invocationExpressions = statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocationExpressions)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                var exceptionTypes = GetExceptionTypes(methodSymbol);

                // Get exceptions from XML documentation
                var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);
                if (xmlExceptionTypes.Any())
                {
                    exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
                }

                foreach (var exceptionType in exceptionTypes)
                {
                    if (exceptionType != null)
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        return exceptions;
    }

    private HashSet<INamedTypeSymbol> GetCaughtExceptions(SyntaxList<CatchClauseSyntax> catchClauses, SemanticModel semanticModel)
    {
        var caughtExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var catchClause in catchClauses)
        {
            if (catchClause.Declaration != null)
            {
                var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                if (catchType != null)
                {
                    caughtExceptions.Add(catchType);
                }
            }
            else
            {
                // General catch clause catches all exceptions
                caughtExceptions = null;
                break;
            }
        }

        return caughtExceptions;
    }

    private bool IsExceptionCaught(INamedTypeSymbol exceptionType, HashSet<INamedTypeSymbol> caughtExceptions)
    {
        if (caughtExceptions == null)
        {
            // General catch clause catches all exceptions
            return true;
        }

        return caughtExceptions.Any(catchType =>
            exceptionType.Equals(catchType, SymbolEqualityComparer.Default) ||
            exceptionType.InheritsFrom(catchType));
    }

    private void AnalyzeAwait(SyntaxNodeAnalysisContext context)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;

        if (awaitExpression.Expression is InvocationExpressionSyntax invocation)
        {
            // Get the invoked symbol
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

            AnalyzeMemberExceptions(context, invocation, methodSymbol);
        }
        else if (awaitExpression.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            AnalyzeIdentifierAndMemberAccess(context, memberAccess);
        }
        else if (awaitExpression.Expression is IdentifierNameSyntax identifier)
        {
            AnalyzeIdentifierAndMemberAccess(context, identifier);
        }
    }

    /// <summary>
    /// Determines if a node is within a catch block.
    /// </summary>
    private bool IsWithinCatchBlock(SyntaxNode node, out CatchClauseSyntax catchClause)
    {
        catchClause = node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        return catchClause != null;
    }

    /// <summary>
    /// Analyzes throw expressions to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeThrowExpression(SyntaxNodeAnalysisContext context)
    {
        var throwExpression = (ThrowExpressionSyntax)context.Node;

        if (throwExpression.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            AnalyzeExceptionThrowingNode(context, throwExpression, exceptionType);
        }
    }

    /// <summary>
    /// Analyzes method calls to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Parent is AwaitExpressionSyntax)
        {
            // Handled in other method.
            return;
        }

        // Get the invoked symbol
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

        AnalyzeMemberExceptions(context, invocation, methodSymbol);
    }

    /// <summary>
    /// Resolves the target method symbol from a delegate, lambda, or method group.
    /// </summary>
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
                        // Handle lambdas
                        if (initializer is AnonymousFunctionExpressionSyntax anonymousFunction)
                        {
                            var lambdaSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
                            if (lambdaSymbol != null)
                                return lambdaSymbol;
                        }

                        // Handle method groups
                        if (initializer is IdentifierNameSyntax || initializer is MemberAccessExpressionSyntax)
                        {
                            var methodGroupSymbol = context.SemanticModel.GetSymbolInfo(initializer).Symbol as IMethodSymbol;
                            if (methodGroupSymbol != null)
                                return methodGroupSymbol;
                        }

                        // Get the method symbol of the initializer (lambda or method group)
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

    /// <summary>
    /// Analyzes object creation expressions to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        var constructorSymbol = context.SemanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
        if (constructorSymbol == null)
            return;

        AnalyzeMemberExceptions(context, objectCreation, constructorSymbol);
    }

    /// <summary>
    /// Analyzes member access expressions (e.g., property accessors) for exception handling.
    /// </summary>
    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        AnalyzeIdentifierAndMemberAccess(context, memberAccess);
    }

    /// <summary>
    /// Analyzes identifier names (e.g. local variables or property accessors in context) for exception handling.
    /// </summary>
    private void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifierName = (IdentifierNameSyntax)context.Node;

        // Ignore identifiers that are part of await expression
        if (identifierName.Parent is AwaitExpressionSyntax)
            return;

        // Ignore identifiers that are part of member access
        if (identifierName.Parent is MemberAccessExpressionSyntax)
            return;

        AnalyzeIdentifierAndMemberAccess(context, identifierName);
    }

    private void AnalyzeIdentifierAndMemberAccess(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(expression).Symbol as IPropertySymbol;
        if (symbol == null)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertyExceptions(context, expression, symbol);
        }
    }

    /// <summary>
    /// Analyzes element access expressions (e.g., indexers) for exception handling.
    /// </summary>
    private void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        var symbol = context.SemanticModel.GetSymbolInfo(elementAccess).Symbol as IPropertySymbol;
        if (symbol == null)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertyExceptions(context, elementAccess, symbol);
        }
    }

    /// <summary>
    /// Analyzes event assignments (e.g., += or -=) for exception handling.
    /// </summary>
    private void AnalyzeEventAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        var eventSymbol = context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IEventSymbol;
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
            AnalyzeMemberExceptions(context, assignment, methodSymbol);
        }
    }

    /// <summary>
    /// Analyzes exceptions thrown by a property, specifically its getters and setters.
    /// </summary>
    private void AnalyzePropertyExceptions(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, IPropertySymbol propertySymbol)
    {
        // Determine if the analyzed expression is for a getter or setter
        bool isGetter = IsPropertyGetter(expression);
        bool isSetter = IsPropertySetter(expression);

        // List to collect all relevant exception types
        var exceptionTypes = new List<INamedTypeSymbol?>();

        // Retrieve exception types documented in XML comments for the property
        var xmlDocumentedExceptions = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, propertySymbol);

        // Filter exceptions documented specifically for the getter and setter
        var getterExceptions = xmlDocumentedExceptions.Where(x =>
            x.Description.Contains(" get ") ||
            x.Description.Contains(" gets ") ||
            x.Description.Contains(" getting "));

        var setterExceptions = xmlDocumentedExceptions.Where(x =>
            x.Description.Contains(" set ") ||
            x.Description.Contains(" sets ") ||
            x.Description.Contains(" setting "));

        // Handle exceptions that don't explicitly belong to getters or setters
        var allOtherExceptions = xmlDocumentedExceptions
            .Except(getterExceptions)
            .Except(setterExceptions);

        // Analyze exceptions thrown by the getter if applicable
        if (isGetter && propertySymbol.GetMethod != null)
        {
            var getterMethodExceptions = GetExceptionTypes(propertySymbol.GetMethod);
            exceptionTypes.AddRange(getterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(getterMethodExceptions);
        }

        // Analyze exceptions thrown by the setter if applicable
        if (isSetter && propertySymbol.SetMethod != null)
        {
            var setterMethodExceptions = GetExceptionTypes(propertySymbol.SetMethod);
            exceptionTypes.AddRange(setterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(setterMethodExceptions);
        }

        // Add other exceptions not specific to getters or setters
        exceptionTypes.AddRange(allOtherExceptions.Select(x => x.ExceptionType));

        // Deduplicate and analyze each distinct exception type
        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, expression, exceptionType);
        }
    }

    /// <summary>
    /// Analyzes exceptions thrown by a method, constructor, or other member.
    /// </summary>
    private void AnalyzeMemberExceptions(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol)
    {
        if (methodSymbol == null)
            return;

        List<INamedTypeSymbol?> exceptionTypes = GetExceptionTypes(methodSymbol);

        // Get exceptions from XML documentation
        var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, methodSymbol);

        if (xmlExceptionTypes.Any())
        {
            exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
        }

        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, node, exceptionType);
        }
    }

    private static List<INamedTypeSymbol?> GetExceptionTypes(IMethodSymbol methodSymbol)
    {
        // Get exceptions from Throws attributes
        var exceptionAttributes = GetThrowsAttributes(methodSymbol);

        // Get specific exception types from Throws attributes
        var exceptionTypes = exceptionAttributes
            .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
            .Where(type => type != null)
            .ToList();
        return exceptionTypes;
    }

    private static List<AttributeData> GetThrowsAttributes(ISymbol symbol)
    {
        return GetThrowsAttributes(symbol.GetAttributes());
    }

    private static List<AttributeData> GetThrowsAttributes(IEnumerable<AttributeData> attributes)
    {
        return attributes
                    .Where(attr => attr.AttributeClass?.Name == "ThrowsAttribute")
                    .ToList();
    }

    /// <summary>
    /// Determines if a catch clause handles the specified exception type.
    /// </summary>
    private bool CatchClauseHandlesException(CatchClauseSyntax catchClause, SemanticModel semanticModel, INamedTypeSymbol exceptionType)
    {
        if (catchClause.Declaration == null)
            return true; // Catch-all handles all exceptions

        var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
        if (catchType == null)
            return false;

        // Check if the exceptionType matches or inherits from the catchType
        return exceptionType.Equals(catchType, SymbolEqualityComparer.Default) ||
               exceptionType.InheritsFrom(catchType);
    }

    /// <summary>
    /// Determines if an exception is handled by any enclosing try-catch blocks.
    /// </summary>
    private bool IsExceptionHandled(SyntaxNode node, INamedTypeSymbol exceptionType, SemanticModel semanticModel)
    {
        SyntaxNode? prevNode = null;

        var current = node.Parent;
        while (current != null)
        {
            if (current is TryStatementSyntax tryStatement)
            {
                // Prevents analysis within the first try-catch,
                // when coming from either a catch clause or a finally clause. 

                var notComingFromCatchOrFinally = prevNode is not null
                    && !tryStatement.Catches.Contains(prevNode)
                    && tryStatement.Finally != prevNode;

                if (notComingFromCatchOrFinally)
                {
                    foreach (var catchClause in tryStatement.Catches)
                    {
                        if (CatchClauseHandlesException(catchClause, semanticModel, exceptionType))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (current is CatchClauseSyntax catchClause)
            {
                if (CatchClauseHandlesException(catchClause, semanticModel, exceptionType))
                {
                    return true;
                }
            }
            else if (current is FinallyClauseSyntax)
            {

            }

            prevNode = current;
            current = current.Parent;
        }

        return false; // Exception is not handled by any enclosing try-catch
    }

    /// <summary>
    /// Analyzes a node that throws or propagates exceptions to check for handling or declaration.
    /// </summary>
    private void AnalyzeExceptionThrowingNode(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol? exceptionType)
    {
        if (exceptionType == null)
            return;

        // Check for general exceptions
        if (IsGeneralException(exceptionType))
        {
            context.ReportDiagnostic(Diagnostic.Create(RuleGeneralThrow, node.GetLocation()));
        }

        // Check if the exception is declared via [Throws]
        var isDeclared = IsExceptionDeclaredInMethod(context, node, exceptionType);

        // Determine if the exception is handled by any enclosing try-catch
        var isHandled = IsExceptionHandled(node, exceptionType, context.SemanticModel);

        // Report diagnostic if neither handled nor declared
        if (!isHandled && !isDeclared)
        {
            var properties = ImmutableDictionary.Create<string, string?>()
                .Add("ExceptionType", exceptionType.Name);

            var diagnostic = Diagnostic.Create(RuleUnhandledException, node.GetLocation(), properties, exceptionType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool IsExceptionDeclaredInMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        foreach (var ancestor in node.Ancestors())
        {
            IMethodSymbol methodSymbol = null;

            switch (ancestor)
            {
                case MethodDeclarationSyntax methodDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
                    break;
                case ConstructorDeclarationSyntax constructorDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(constructorDeclaration) as IMethodSymbol;
                    break;
                case AccessorDeclarationSyntax accessorDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(accessorDeclaration) as IMethodSymbol;
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(localFunction) as IMethodSymbol;
                    break;
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    methodSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
                    break;
                default:
                    continue;
            }

            if (methodSymbol != null)
            {
                if (IsExceptionDeclaredInSymbol(methodSymbol, exceptionType))
                    return true;
            }
        }

        return false;
    }

    private bool IsExceptionDeclaredInSymbol(IMethodSymbol methodSymbol, INamedTypeSymbol exceptionType)
    {
        if (methodSymbol == null)
            return false;

        // Retrieve all [Throws] attributes
        var throwsAttributes = GetThrowsAttributes(methodSymbol);

        foreach (var attribute in throwsAttributes)
        {
            // Ensure the attribute has at least one constructor argument
            if (attribute.ConstructorArguments.Length == 0)
                continue;

            var exceptionTypeArg = attribute.ConstructorArguments[0];
            if (exceptionTypeArg.Kind != TypedConstantKind.Type)
                continue;

            var declaredExceptionType = exceptionTypeArg.Value as INamedTypeSymbol;
            if (declaredExceptionType == null)
                continue;

            if (exceptionType.Equals(declaredExceptionType, SymbolEqualityComparer.Default))
                return true;

            // Check if the declared exception is a base type of the thrown exception
            if (exceptionType.InheritsFrom(declaredExceptionType))
                return true;
        }

        return false;
    }

    private bool IsGeneralException(INamedTypeSymbol exceptionType)
    {
        return exceptionType.ToDisplayString() == "System.Exception";
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
