namespace Sundstrom.CheckedExceptions;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class CheckedExceptionsAnalyzer : DiagnosticAnalyzer
{
    static readonly ConcurrentDictionary<AnalyzerOptions, AnalyzerSettings> configs = new ConcurrentDictionary<AnalyzerOptions, AnalyzerSettings>();

    // Diagnostic IDs
    public const string DiagnosticIdUnhandled = "THROW001";
    public const string DiagnosticIdIgnoredException = "THROW002";
    public const string DiagnosticIdGeneralThrows = "THROW003";
    public const string DiagnosticIdGeneralThrow = "THROW004";
    public const string DiagnosticIdDuplicateDeclarations = "THROW005";
    public const string DiagnosticIdMissingThrowsOnBaseMember = "THROW006";

    public static IEnumerable<string> AllDiagnosticsIds = [DiagnosticIdUnhandled, DiagnosticIdGeneralThrows, DiagnosticIdGeneralThrow, DiagnosticIdDuplicateDeclarations];

    private static readonly DiagnosticDescriptor RuleUnhandledException = new(
        DiagnosticIdUnhandled,
        "Unhandled exception",
        "Exception '{0}' {1} thrown but not handled",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleIgnoredException = new DiagnosticDescriptor(
        DiagnosticIdIgnoredException,
        "Ignored exception may cause runtime issues",
        "Exception '{0}' is ignored by configuration but may cause runtime issues if unhandled",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleGeneralThrow = new(
        DiagnosticIdGeneralThrow,
        "Avoid throwing 'Exception'",
        "Throwing 'Exception' is too general; use a more specific exception type instead",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleGeneralThrows = new DiagnosticDescriptor(
        DiagnosticIdGeneralThrows,
        "Avoid declaring exception type 'Exception'",
        "Declaring 'Exception' is too general; use a more specific exception type instead",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleDuplicateDeclarations = new DiagnosticDescriptor(
        DiagnosticIdDuplicateDeclarations,
        "Avoid duplicate declarations of the same exception type",
        "Duplicate declarations of the exception type '{0}' found. Remove them to avoid redundancy.",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuleMissingThrowsOnBaseMember = new DiagnosticDescriptor(
        DiagnosticIdMissingThrowsOnBaseMember,
        "Missing Throws declaration",
        "'{0}' is not declaring exception '{1}' that is declared by an overriding or implementing member",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Base or interface members should declare compatible exceptions when overridden or implemented members declare them using [Throws].");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RuleUnhandledException, RuleIgnoredException, RuleGeneralThrows, RuleGeneralThrow, RuleDuplicateDeclarations, RuleMissingThrowsOnBaseMember];

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

    private const string SettingsFileName = "CheckedExceptions.settings.json";

    private AnalyzerSettings GetAnalyzerSettings(AnalyzerOptions analyzerOptions)
    {
        if (!configs.TryGetValue(analyzerOptions, out var config))
        {
            foreach (var additionalFile in analyzerOptions.AdditionalFiles)
            {
                if (Path.GetFileName(additionalFile.Path).Equals(SettingsFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var text = additionalFile.GetText();
                    if (text is not null)
                    {
                        var json = text.ToString();
                        config = JsonSerializer.Deserialize<AnalyzerSettings>(json);
                        break;
                    }
                }
            }

            config ??= new AnalyzerSettings(); // Return default options if config file is not found

            configs.TryAdd(analyzerOptions, config);
        }

        return config ?? new AnalyzerSettings();
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

        if (throwsAttributes.Count is 0)
            return;

        CheckForGeneralExceptionThrows(context, throwsAttributes);

        if (throwsAttributes.Any())
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
        if (attributeSymbol is null)
            return false;

        var attributeType = attributeSymbol.ContainingType;
        return attributeType.Name is "ThrowsAttribute";
    }

    private void AnalyzeMethodSymbol(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol is null)
            return;

        var throwsAttributes = GetThrowsAttributes(methodSymbol).ToImmutableArray();

        CheckForCompatibilityWithBaseOrInterface(context, throwsAttributes);

        if (throwsAttributes.Length == 0)
            return;

        CheckForGeneralExceptionThrows(throwsAttributes, context);
        CheckForDuplicateThrowsAttributes(context, throwsAttributes);
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

        var exceptionTypes = GetDistictExceptionTypes(attribute);
        return exceptionTypes.Any(exceptionType => exceptionType?.Name == exceptionTypeName);
    }

    public static IEnumerable<INamedTypeSymbol> GetExceptionTypes(params IEnumerable<AttributeData> exceptionAttributes)
    {
        var constructorArguments = exceptionAttributes
            .SelectMany(attr => attr.ConstructorArguments);

        foreach (var arg in constructorArguments)
        {
            if (arg.Kind is TypedConstantKind.Array)
            {
                foreach (var t in arg.Values)
                {
                    if (t.Kind is TypedConstantKind.Type)
                    {
                        yield return (INamedTypeSymbol)t.Value!;
                    }
                }
            }
            else if (arg.Kind is TypedConstantKind.Type)
            {
                yield return (INamedTypeSymbol)arg.Value!;
            }
        }
    }

    public static IEnumerable<INamedTypeSymbol> GetDistictExceptionTypes(params IEnumerable<AttributeData> exceptionAttributes)
    {
        var exceptionTypes = GetExceptionTypes(exceptionAttributes);

        return exceptionTypes.Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>();
    }

    /// <summary>
    /// Analyzes throw statements to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var throwStatement = (ThrowStatementSyntax)context.Node;

        // Handle rethrows (throw;)
        if (throwStatement.Expression is null)
        {
            if (IsWithinCatchBlock(throwStatement, out var catchClause))
            {
                if (catchClause is not null)
                {
                    if (catchClause.Declaration is null)
                    {
                        // General catch block with 'throw;'
                        // Analyze exceptions thrown in the try block
                        var tryStatement = catchClause.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault();
                        if (tryStatement is not null)
                        {
                            AnalyzeExceptionsInTryBlock(context, tryStatement, catchClause, throwStatement, settings);
                        }
                    }
                    else
                    {
                        var exceptionType = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                        AnalyzeExceptionThrowingNode(context, throwStatement, exceptionType, settings);
                    }
                }
            }
            return; // No further analysis for rethrows
        }

        // Handle throw new ExceptionType()
        if (throwStatement.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            AnalyzeExceptionThrowingNode(context, throwStatement, exceptionType, settings);
        }
    }

    private void AnalyzeExceptionsInTryBlock(SyntaxNodeAnalysisContext context, TryStatementSyntax tryStatement, CatchClauseSyntax generalCatchClause, ThrowStatementSyntax throwStatement, AnalyzerSettings settings)
    {
        var semanticModel = context.SemanticModel;

        // Collect exceptions that can be thrown in the try block
        var thrownExceptions = CollectUnhandledExceptions(context, tryStatement.Block, settings);

        // Collect exception types handled by preceding catch clauses
        var handledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var catchClause in tryStatement.Catches)
        {
            if (catchClause == generalCatchClause)
                break; // Stop at the general catch clause

            if (catchClause.Declaration is not null)
            {
                var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                if (catchType is not null)
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

        if (handledExceptions is null)
        {
            // All exceptions are handled by a previous general catch
            return;
        }

        // For each thrown exception, check if it is handled
        foreach (var exceptionType in thrownExceptions.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var exceptionName = exceptionType.ToDisplayString();

            if (settings.IgnoredExceptions.Contains(exceptionName))
            {
                // Completely ignore this exception
                continue;
            }
            else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
            {
                if (ShouldIgnore(throwStatement, mode))
                {
                    // Report as THROW002 (Info level)
                    var diagnostic = Diagnostic.Create(RuleIgnoredException, throwStatement.GetLocation(), exceptionType.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }
            }

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
                    exceptionType.Name,
                    THROW001Verbs.MightBe);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private HashSet<INamedTypeSymbol> CollectUnhandledExceptions(SyntaxNodeAnalysisContext context, BlockSyntax block, AnalyzerSettings settings)
    {
        var unhandledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (statement is TryStatementSyntax tryStatement)
            {
                // Recursively collect exceptions from the inner try block
                var innerUnhandledExceptions = CollectUnhandledExceptions(context, tryStatement.Block, settings);

                // Remove exceptions that are caught by the inner catch clauses
                var caughtExceptions = GetCaughtExceptions(tryStatement.Catches, context.SemanticModel);
                innerUnhandledExceptions.RemoveWhere(exceptionType =>
                    IsExceptionCaught(exceptionType, caughtExceptions));

                // Add any exceptions that are not handled in the inner try block
                unhandledExceptions.UnionWith(innerUnhandledExceptions);
            }
            else
            {
                // Collect exceptions thrown in this statement
                var statementExceptions = CollectExceptionsFromStatement(context, statement, settings);

                // Add them to the unhandled exceptions
                unhandledExceptions.UnionWith(statementExceptions);
            }
        }

        return unhandledExceptions;
    }

    private HashSet<INamedTypeSymbol> CollectExceptionsFromStatement(SyntaxNodeAnalysisContext context, StatementSyntax statement, AnalyzerSettings settings)
    {
        SemanticModel semanticModel = context.SemanticModel;

        var exceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Collect exceptions from throw statements
        var throwStatements = statement.DescendantNodesAndSelf().OfType<ThrowStatementSyntax>();
        foreach (var throwStatement in throwStatements)
        {
            if (throwStatement.Expression is not null)
            {
                var exceptionType = semanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;
                if (exceptionType is not null)
                {
                    if (ShouldIncludeException(exceptionType, throwStatement, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        // Collect exceptions from throw expressions
        var throwExpressions = statement.DescendantNodesAndSelf().OfType<ThrowExpressionSyntax>();
        foreach (var throwExpression in throwExpressions)
        {
            if (throwExpression.Expression is not null)
            {
                var exceptionType = semanticModel.GetTypeInfo(throwExpression.Expression).Type as INamedTypeSymbol;
                if (exceptionType is not null)
                {
                    if (ShouldIncludeException(exceptionType, throwExpression, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        // Collect exceptions from method calls and other expressions
        var invocationExpressions = statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocationExpressions)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol is not null)
            {
                var exceptionTypes = GetExceptionTypes(methodSymbol);

                // Get exceptions from XML documentation
                var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);

                xmlExceptionTypes = ProcessNullable(context, invocation, methodSymbol, xmlExceptionTypes);

                if (xmlExceptionTypes.Any())
                {
                    exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
                }

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, invocation, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        var objectCreations = statement.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>();
        foreach (var objectCreation in objectCreations)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
            if (methodSymbol is not null)
            {
                var exceptionTypes = GetExceptionTypes(methodSymbol);

                // Get exceptions from XML documentation
                var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);

                xmlExceptionTypes = ProcessNullable(context, objectCreation, methodSymbol, xmlExceptionTypes);

                if (xmlExceptionTypes.Any())
                {
                    exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
                }

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, objectCreation, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        // Collect from MemberAccess and Identifier
        var memberAccessExpressions = statement.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>();
        foreach (var memberAccess in memberAccessExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(context, memberAccess, propertySymbol);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, memberAccess, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        var elementAccessExpressions = statement.DescendantNodesAndSelf().OfType<ElementAccessExpressionSyntax>();
        foreach (var elementAccess in elementAccessExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(elementAccess).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(context, elementAccess, propertySymbol);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, elementAccess, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        var identifierExpressions = statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifierExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(identifier).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(context, identifier, propertySymbol);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (exceptionType is not null)
                    {
                        if (ShouldIncludeException(exceptionType, identifier, settings))
                        {
                            exceptions.Add(exceptionType);
                        }
                    }
                }
            }
        }

        return exceptions;
    }

    public bool ShouldIncludeException(INamedTypeSymbol exceptionType, SyntaxNode node, AnalyzerSettings settings)
    {
        var exceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var exceptionName = exceptionType.ToDisplayString();

        if (settings.IgnoredExceptions.Contains(exceptionName))
        {
            // Completely ignore this exception
            return false;
        }
        else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
        {
            if (ShouldIgnore(node, mode))
            {
                return false;
            }
        }

        return true;
    }

    private HashSet<INamedTypeSymbol> GetCaughtExceptions(SyntaxList<CatchClauseSyntax> catchClauses, SemanticModel semanticModel)
    {
        var caughtExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var catchClause in catchClauses)
        {
            if (catchClause.Declaration is not null)
            {
                var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                if (catchType is not null)
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
        if (caughtExceptions is null)
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
        var settings = GetAnalyzerSettings(context.Options);

        var awaitExpression = (AwaitExpressionSyntax)context.Node;

        if (awaitExpression.Expression is InvocationExpressionSyntax invocation)
        {
            // Get the invoked symbol
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

            if (methodSymbol is null)
                return;

            // Handle delegate invokes by getting the target method symbol
            if (methodSymbol.MethodKind == MethodKind.DelegateInvoke)
            {
                var targetMethodSymbol = GetTargetMethodSymbol(context, invocation);
                if (targetMethodSymbol is not null)
                {
                    methodSymbol = targetMethodSymbol;
                }
                else
                {
                    // Could not find the target method symbol
                    return;
                }
            }

            AnalyzeMemberExceptions(context, invocation, methodSymbol, settings);
        }
        else if (awaitExpression.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            AnalyzeIdentifierAndMemberAccess(context, memberAccess, settings);
        }
        else if (awaitExpression.Expression is IdentifierNameSyntax identifier)
        {
            AnalyzeIdentifierAndMemberAccess(context, identifier, settings);
        }
    }

    /// <summary>
    /// Determines if a node is within a catch block.
    /// </summary>
    private bool IsWithinCatchBlock(SyntaxNode node, out CatchClauseSyntax catchClause)
    {
        catchClause = node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        return catchClause is not null;
    }

    /// <summary>
    /// Analyzes throw expressions to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeThrowExpression(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var throwExpression = (ThrowExpressionSyntax)context.Node;

        if (throwExpression.Expression is ObjectCreationExpressionSyntax creationExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(creationExpression).Type as INamedTypeSymbol;
            AnalyzeExceptionThrowingNode(context, throwExpression, exceptionType, settings);
        }
    }

    /// <summary>
    /// Analyzes method calls to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Parent is AwaitExpressionSyntax)
        {
            // Handled in other method.
            return;
        }

        // Get the invoked symbol
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol is null)
            return;

        // Handle delegate invokes by getting the target method symbol
        if (methodSymbol.MethodKind == MethodKind.DelegateInvoke)
        {
            var targetMethodSymbol = GetTargetMethodSymbol(context, invocation);
            if (targetMethodSymbol is not null)
            {
                methodSymbol = targetMethodSymbol;
            }
            else
            {
                // Could not find the target method symbol
                return;
            }
        }

        AnalyzeMemberExceptions(context, invocation, methodSymbol, settings);
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

        if (symbol is null)
            return null;

        if (symbol is ILocalSymbol localSymbol)
        {
            // Get the syntax node where the local variable is declared
            var declaringSyntaxReference = localSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (declaringSyntaxReference is not null)
            {
                var syntaxNode = declaringSyntaxReference.GetSyntax();

                if (syntaxNode is VariableDeclaratorSyntax variableDeclarator)
                {
                    var initializer = variableDeclarator.Initializer?.Value;

                    if (initializer is not null)
                    {
                        // Handle lambdas
                        if (initializer is AnonymousFunctionExpressionSyntax anonymousFunction)
                        {
                            var lambdaSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
                            if (lambdaSymbol is not null)
                                return lambdaSymbol;
                        }

                        // Handle method groups
                        if (initializer is IdentifierNameSyntax || initializer is MemberAccessExpressionSyntax)
                        {
                            var methodGroupSymbol = context.SemanticModel.GetSymbolInfo(initializer).Symbol as IMethodSymbol;
                            if (methodGroupSymbol is not null)
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
        var settings = GetAnalyzerSettings(context.Options);

        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        var constructorSymbol = context.SemanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
        if (constructorSymbol is null)
            return;

        AnalyzeMemberExceptions(context, objectCreation, constructorSymbol, settings);
    }

    /// <summary>
    /// Analyzes member access expressions (e.g., property accessors) for exception handling.
    /// </summary>
    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        AnalyzeIdentifierAndMemberAccess(context, memberAccess, settings);
    }

    /// <summary>
    /// Analyzes identifier names (e.g. local variables or property accessors in context) for exception handling.
    /// </summary>
    private void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var identifierName = (IdentifierNameSyntax)context.Node;

        // Ignore identifiers that are part of await expression
        if (identifierName.Parent is AwaitExpressionSyntax)
            return;

        // Ignore identifiers that are part of member access
        if (identifierName.Parent is MemberAccessExpressionSyntax)
            return;

        AnalyzeIdentifierAndMemberAccess(context, identifierName, settings);
    }

    private void AnalyzeIdentifierAndMemberAccess(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, AnalyzerSettings settings)
    {
        var s = context.SemanticModel.GetSymbolInfo(expression).Symbol;
        var symbol = s as IPropertySymbol;
        if (symbol is null)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertyExceptions(context, expression, symbol, settings);
        }
    }

    /// <summary>
    /// Analyzes element access expressions (e.g., indexers) for exception handling.
    /// </summary>
    private void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        var symbol = context.SemanticModel.GetSymbolInfo(elementAccess).Symbol as IPropertySymbol;
        if (symbol is null)
            return;

        if (symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertyExceptions(context, elementAccess, symbol, settings);
        }
    }

    /// <summary>
    /// Analyzes event assignments (e.g., += or -=) for exception handling.
    /// </summary>
    private void AnalyzeEventAssignment(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var assignment = (AssignmentExpressionSyntax)context.Node;

        var eventSymbol = context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IEventSymbol;
        if (eventSymbol is null)
            return;

        // Get the method symbol for the add or remove accessor
        IMethodSymbol? methodSymbol = null;

        if (assignment.IsKind(SyntaxKind.AddAssignmentExpression) && eventSymbol.AddMethod is not null)
        {
            methodSymbol = eventSymbol.AddMethod;
        }
        else if (assignment.IsKind(SyntaxKind.SubtractAssignmentExpression) && eventSymbol.RemoveMethod is not null)
        {
            methodSymbol = eventSymbol.RemoveMethod;
        }

        if (methodSymbol is not null)
        {
            AnalyzeMemberExceptions(context, assignment, methodSymbol, settings);
        }
    }

    /// <summary>
    /// Analyzes exceptions thrown by a property, specifically its getters and setters.
    /// </summary>
    private void AnalyzePropertyExceptions(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, IPropertySymbol propertySymbol,
        AnalyzerSettings settings)
    {
        HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(context, expression, propertySymbol);

        // Deduplicate and analyze each distinct exception type
        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, expression, exceptionType, settings);
        }
    }

    private HashSet<INamedTypeSymbol> GetPropertyExceptionTypes(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, IPropertySymbol propertySymbol)
    {
        // Determine if the analyzed expression is for a getter or setter
        bool isGetter = IsPropertyGetter(expression);
        bool isSetter = IsPropertySetter(expression);

        // List to collect all relevant exception types
        var exceptionTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Retrieve exception types documented in XML comments for the property
        var xmlDocumentedExceptions = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, propertySymbol);

        // Filter exceptions documented specifically for the getter and setter
        var getterExceptions = xmlDocumentedExceptions.Where(x =>
            x.Description.Contains(" get ") ||
            x.Description.Contains(" gets ") ||
            x.Description.Contains(" getting ") ||
            x.Description.Contains(" retrieved "));

        var setterExceptions = xmlDocumentedExceptions.Where(x =>
            x.Description.Contains(" set ") ||
            x.Description.Contains(" sets ") ||
            x.Description.Contains(" setting "));

        if (isSetter && propertySymbol.SetMethod is not null)
        {
            // Will filter away 
            setterExceptions = ProcessNullable(context, expression, propertySymbol.SetMethod, setterExceptions);
        }

        // Handle exceptions that don't explicitly belong to getters or setters
        var allOtherExceptions = xmlDocumentedExceptions
            .Except(getterExceptions);
        allOtherExceptions = allOtherExceptions
            .Except(setterExceptions);

        if (isSetter && propertySymbol.SetMethod is not null)
        {
            allOtherExceptions = ProcessNullable(context, expression, propertySymbol.SetMethod, allOtherExceptions);
        }

        // Analyze exceptions thrown by the getter if applicable
        if (isGetter && propertySymbol.GetMethod is not null)
        {
            var getterMethodExceptions = GetExceptionTypes(propertySymbol.GetMethod);
            exceptionTypes.AddRange(getterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(getterMethodExceptions);
        }

        // Analyze exceptions thrown by the setter if applicable
        if (isSetter && propertySymbol.SetMethod is not null)
        {
            var setterMethodExceptions = GetExceptionTypes(propertySymbol.SetMethod);
            exceptionTypes.AddRange(setterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(setterMethodExceptions);
        }

        if (propertySymbol.GetMethod is not null)
        {
            allOtherExceptions = ProcessNullable(context, expression, propertySymbol.GetMethod, allOtherExceptions);
        }

        // Add other exceptions not specific to getters or setters
        exceptionTypes.AddRange(allOtherExceptions.Select(x => x.ExceptionType));
        return exceptionTypes;
    }

    /// <summary>
    /// Analyzes exceptions thrown by a method, constructor, or other member.
    /// </summary>
    private void AnalyzeMemberExceptions(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol,
        AnalyzerSettings settings)
    {
        if (methodSymbol is null)
            return;

        List<INamedTypeSymbol> exceptionTypes = GetExceptionTypes(methodSymbol);

        // Get exceptions from XML documentation
        var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, methodSymbol);

        xmlExceptionTypes = ProcessNullable(context, node, methodSymbol, xmlExceptionTypes);

        if (xmlExceptionTypes.Any())
        {
            exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
        }

        exceptionTypes = ProcessNullable(context, node, methodSymbol, exceptionTypes).ToList();

        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, node, exceptionType, settings);
        }
    }

    static INamedTypeSymbol? argumentNullExceptionTypeSymbol;

    private static IEnumerable<ExceptionInfo> ProcessNullable(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol, IEnumerable<ExceptionInfo> exceptionInfos)
    {
        if (argumentNullExceptionTypeSymbol is null)
        {
            argumentNullExceptionTypeSymbol = context.Compilation.GetTypeByMetadataName("System.ArgumentNullException");
        }

        var isCompilationNullableEnabled = context.Compilation.Options.NullableContextOptions is NullableContextOptions.Enable;

        var nullableContext = context.SemanticModel.GetNullableContext(node.SpanStart);
        var isNodeInNullableContext = nullableContext is NullableContext.Enabled;

        if (isNodeInNullableContext || isCompilationNullableEnabled)
        {
            if (methodSymbol.IsExtensionMethod)
            {
                return exceptionInfos.Where(x => !x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
            }

            if (methodSymbol.Parameters.Count() is 1)
            {
                var p = methodSymbol.Parameters.First();

                if (p.NullableAnnotation is NullableAnnotation.NotAnnotated)
                {
                    return exceptionInfos.Where(x => !x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
                }
            }
            else
            {
                exceptionInfos = exceptionInfos.Where(x =>
                {
                    var p = methodSymbol.Parameters.FirstOrDefault(p => x.Parameters.Any(p2 => p.Name == p2.Name));
                    if (p is not null)
                    {
                        if (x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default)
                        && p.NullableAnnotation is NullableAnnotation.NotAnnotated)
                        {
                            return false;
                        }
                    }
                    return true;
                }).ToList();
            }
        }

        return exceptionInfos;
    }

    private static IEnumerable<INamedTypeSymbol> ProcessNullable(SyntaxNodeAnalysisContext context, SyntaxNode node, IMethodSymbol methodSymbol, IEnumerable<INamedTypeSymbol> exceptions)
    {
        if (argumentNullExceptionTypeSymbol is null)
        {
            argumentNullExceptionTypeSymbol = context.Compilation.GetTypeByMetadataName("System.ArgumentNullException");
        }

        var isCompilationNullableEnabled = context.Compilation.Options.NullableContextOptions is NullableContextOptions.Enable;

        var nullableContext = context.SemanticModel.GetNullableContext(node.SpanStart);
        var isNodeInNullableContext = nullableContext is NullableContext.Enabled;

        if (isNodeInNullableContext || isCompilationNullableEnabled)
        {
            return exceptions.Where(x => !x.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
        }

        return exceptions;
    }

    private static List<INamedTypeSymbol> GetExceptionTypes(IMethodSymbol methodSymbol)
    {
        // Get exceptions from Throws attributes
        var exceptionAttributes = GetThrowsAttributes(methodSymbol);

        return GetDistictExceptionTypes(exceptionAttributes).ToList();
    }

    private static List<AttributeData> GetThrowsAttributes(ISymbol symbol)
    {
        return GetThrowsAttributes(symbol.GetAttributes());
    }

    private static List<AttributeData> GetThrowsAttributes(IEnumerable<AttributeData> attributes)
    {
        return attributes
                    .Where(attr => attr.AttributeClass?.Name is "ThrowsAttribute")
                    .ToList();
    }

    /// <summary>
    /// Determines if a catch clause handles the specified exception type.
    /// </summary>
    private bool CatchClauseHandlesException(CatchClauseSyntax catchClause, SemanticModel semanticModel, INamedTypeSymbol exceptionType)
    {
        if (catchClause.Declaration is null)
            return true; // Catch-all handles all exceptions

        var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
        if (catchType is null)
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
        while (current is not null)
        {
            // Stop here since the throwing node is within a lambda or a local function
            // and the boundary has been reached.
            if (current is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax)
            {
                return false;
            }

            if (current is TryStatementSyntax tryStatement)
            {
                // Prevents analysis within the first try-catch,
                // when coming from either a catch clause or a finally clause. 

                // Skip if the node is within a catch or finally block of the current try statement
                bool isInCatchOrFinally = tryStatement.Catches.Any(c => c.Contains(node)) ||
                                          (tryStatement.Finally is not null && tryStatement.Finally.Contains(node));


                if (!isInCatchOrFinally)
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

            prevNode = current;
            current = current.Parent;
        }

        return false; // Exception is not handled by any enclosing try-catch
    }

    /// <summary>
    /// Analyzes a node that throws or propagates exceptions to check for handling or declaration.
    /// </summary>
    private void AnalyzeExceptionThrowingNode(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        INamedTypeSymbol? exceptionType,
        AnalyzerSettings settings)
    {
        if (exceptionType is null)
            return;

        var exceptionName = exceptionType.ToDisplayString();

        if (settings.IgnoredExceptions.Contains(exceptionName))
        {
            // Completely ignore this exception
            return;
        }
        else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
        {
            if (ShouldIgnore(node, mode))
            {
                // Report as THROW002 (Info level)
                var diagnostic = Diagnostic.Create(RuleIgnoredException, node.GetLocation(), exceptionType.Name);
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }

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

            var isThrowingConstruct = node is ThrowStatementSyntax or ThrowExpressionSyntax;

            var verb = isThrowingConstruct ? THROW001Verbs.Is : THROW001Verbs.MightBe;

            var diagnostic = Diagnostic.Create(RuleUnhandledException, node.GetLocation(), properties, exceptionType.Name, verb);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool ShouldIgnore(SyntaxNode node, ExceptionMode mode)
    {
        if (mode is ExceptionMode.Always)
            return true;

        if (mode is ExceptionMode.Throw && node is ThrowStatementSyntax or ThrowExpressionSyntax)
            return true;

        if (mode is ExceptionMode.Propagation && node
            is MemberAccessExpressionSyntax
            or IdentifierNameSyntax
            or InvocationExpressionSyntax)
            return true;

        return false;
    }

    private bool IsExceptionDeclaredInMethod(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        foreach (var ancestor in node.Ancestors())
        {
            IMethodSymbol? methodSymbol = null;

            switch (ancestor)
            {
                case MethodDeclarationSyntax methodDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                    break;
                case ConstructorDeclarationSyntax constructorDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(constructorDeclaration);
                    break;
                case AccessorDeclarationSyntax accessorDeclaration:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(accessorDeclaration);
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    methodSymbol = context.SemanticModel.GetDeclaredSymbol(localFunction);
                    break;
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    methodSymbol = context.SemanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
                    break;
                default:
                    // Continue up to next node
                    continue;
            }

            if (methodSymbol is not null)
            {
                if (IsExceptionDeclaredInSymbol(methodSymbol, exceptionType))
                    return true;

                if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                {
                    // Break because you are analyzing a local function or anonymous function (lambda)
                    // If you don't then it will got to the method, and it will affect analysis of this inline function.
                    break;
                }
            }
        }

        return false;
    }

    private bool IsExceptionDeclaredInSymbol(IMethodSymbol methodSymbol, INamedTypeSymbol exceptionType)
    {
        if (methodSymbol is null)
            return false;

        var declaredExceptionTypes = GetExceptionTypes(methodSymbol);

        foreach (var declaredExceptionType in declaredExceptionTypes)
        {
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
        return exceptionType.ToDisplayString() is "System.Exception";
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