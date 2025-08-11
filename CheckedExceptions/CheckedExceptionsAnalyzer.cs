namespace Sundstrom.CheckedExceptions;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class CheckedExceptionsAnalyzer : DiagnosticAnalyzer
{
    static readonly ConcurrentDictionary<AnalyzerOptions, AnalyzerSettings> configs = new ConcurrentDictionary<AnalyzerOptions, AnalyzerSettings>();

    // Diagnostic IDs
    public const string DiagnosticIdUnhandled = "THROW001";
    public const string DiagnosticIdIgnoredException = "THROW002";
    public const string DiagnosticIdGeneralThrowDeclared = "THROW003";
    public const string DiagnosticIdGeneralThrow = "THROW004";
    public const string DiagnosticIdDuplicateDeclarations = "THROW005";
    public const string DiagnosticIdMissingThrowsOnBaseMember = "THROW006";
    public const string DiagnosticIdMissingThrowsFromBaseMember = "THROW007";
    public const string DiagnosticIdDuplicateThrowsByHierarchy = "THROW008";
    public const string DiagnosticIdRedundantTypedCatchClause = "THROW009";
    public const string DiagnosticIdThrowsDeclarationNotValidOnFullProperty = "THROW010";
    public const string DiagnosticIdXmlDocButNoThrows = "THROW011";
    public const string DiagnosticIdRedundantExceptionDeclaration = "THROW012";
    public const string DiagnosticIdRedundantCatchAllClause = "THROW013";
    public const string DiagnosticIdCatchHandlesNoRemainingExceptions = "THROW014";
    public const string DiagnosticIdRuleUnreachableCode = "THROW020";
    public const string DiagnosticIdRuleUnreachableCodeHidden = "IDE001";

    public static IEnumerable<string> AllDiagnosticsIds = [DiagnosticIdUnhandled, DiagnosticIdGeneralThrowDeclared, DiagnosticIdGeneralThrow, DiagnosticIdDuplicateDeclarations, DiagnosticIdRuleUnreachableCode, DiagnosticIdRuleUnreachableCodeHidden];

    private static readonly DiagnosticDescriptor RuleUnhandledException = new(
        DiagnosticIdUnhandled,
        "Unhandled exception",
        "Unhandled exception type '{0}'",
        "Control flow",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reports exceptions that are thrown but not caught or declared with [Throws], potentially violating exception safety.");

    private static readonly DiagnosticDescriptor RuleIgnoredException = new DiagnosticDescriptor(
        DiagnosticIdIgnoredException,
        "Ignored exception may cause runtime issues",
        "Exception '{0}' is ignored by configuration but may cause runtime issues if unhandled",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Informs about exceptions excluded from analysis but which may still propagate at runtime if not properly handled.");

    private static readonly DiagnosticDescriptor RuleGeneralThrow = new(
        DiagnosticIdGeneralThrow,
        "Avoid throwing 'Exception'",
        "Avoid throwing 'System.Exception'; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Discourages throwing the base System.Exception type directly, encouraging clearer and more actionable error semantics.");

    private static readonly DiagnosticDescriptor RuleGeneralThrowDeclared = new DiagnosticDescriptor(
        DiagnosticIdGeneralThrowDeclared,
        "Avoid declaring exception type 'System.Exception'",
        "Avoid declaring exception type 'System.Exception'; use a more specific exception type",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Discourages the use of System.Exception in [Throws] attributes. Prefer declaring more specific exception types.");

    private static readonly DiagnosticDescriptor RuleDuplicateDeclarations = new DiagnosticDescriptor(
        DiagnosticIdDuplicateDeclarations,
        "Avoid duplicate declarations of the same exception type",
        "Duplicate declaration of the exception type '{0}' found. Remove it to avoid redundancy.",
        "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects multiple exception declarations for the same exception type on a single member, which is redundant.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleMissingThrowsFromBaseMember = new(
        DiagnosticIdMissingThrowsFromBaseMember,
        "Missing Throws declaration for exception declared on base member",
        "Exception '{1}' declared in '{0}' is not declared in this override or implemented member",
        "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Ensures that overridden or implemented members declare exceptions required by their base or interface definitions.");

    private static readonly DiagnosticDescriptor RuleMissingThrowsOnBaseMember = new(
        DiagnosticIdMissingThrowsOnBaseMember,
        "Incompatible Throws declaration",
        "Exception '{1}' is not compatible with throws declaration(s) in '{0}'",
        "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Ensures that overridden or implemented members do not declare exceptions incompatible with their base or interface definitions.");

    private static readonly DiagnosticDescriptor RuleDuplicateThrowsByHierarchy = new(
        DiagnosticIdDuplicateThrowsByHierarchy,
        title: "Redundant exception declaration",
        messageFormat: "Exception already handled by declaration of super type ('{0}')",
        category: "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects redundant [Throws] declarations where a more general exception type already covers the specific exception.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleRedundantTypedCatchClause = new(
        DiagnosticIdRedundantTypedCatchClause,
        title: "Redundant catch typed clause",
        messageFormat: "Exception type '{0}' is never thrown within the try block",
        category: "Control flow",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects catch clauses for exception types that are never thrown inside the associated try block, making the catch clause redundant.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleRedundantCatchAllClause = new(
        DiagnosticIdRedundantCatchAllClause,
        title: "Redundant catch-all clause",
        messageFormat: "This catch-all clause is redundant because no exceptions remain to be handled",
        category: "Control flow",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reports catch-all clauses that cannot handle any exceptions because all exceptions " +
                     "thrown in the try block are either handled by earlier catch clauses or do not occur.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleThrowsDeclarationNotValidOnFullProperty = new(
        DiagnosticIdThrowsDeclarationNotValidOnFullProperty,
        title: "Throws attribute is not valid on full property declarations",
        messageFormat: "Throws attribute is not valid on full property declarations. Place it on accessors instead.",
        category: "Contract",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [Throws] attribute cannot be applied to full property declarations. Instead, place the attribute on individual accessors (get or set) to indicate which operations may throw exceptions.");

    private static readonly DiagnosticDescriptor RuleXmlDocButNoThrows = new(
        DiagnosticIdXmlDocButNoThrows,
        title: "Exception in XML documentation is not declared with [Throws]",
        messageFormat: "Exception '{0}' is documented in XML documentation but not declared with [Throws]",
        category: "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "This member's XML documentation declares an exception, but it is not declared with a [Throws] attribute. " +
                     "Declare the exception with [Throws] to keep the documentation and the enforced contract consistent.");

    private static readonly DiagnosticDescriptor RuleRedundantExceptionDeclaration = new(
        DiagnosticIdRedundantExceptionDeclaration,
        title: "Redundant exception declaration",
        messageFormat: "Exception '{0}' is declared but never thrown",
        category: "Contract",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects exception types declared with [Throws] that are never thrown in the method or property body, making the declaration redundant.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleCatchHandlesNoRemainingExceptions = new(
        DiagnosticIdCatchHandlesNoRemainingExceptions,
        title: "Catch clause has no remaining exceptions to handle",
        messageFormat: "All matching exceptions for this type are already caught by previous clauses ({0})",
        category: "Control flow",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reports catch clauses that are syntactically valid but will never be executed, "
                + "because all matching exceptions have already been caught by previous clauses.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleUnreachableCode = new(
        DiagnosticIdRuleUnreachableCode,
        title: "Unreachable code",
        messageFormat: "Unreachable code detected",
        category: "Control flow",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects throw statements that cannot be reached due to surrounding control flow or exception handling.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    private static readonly DiagnosticDescriptor RuleUnreachableCodeHidden = new(
        DiagnosticIdRuleUnreachableCodeHidden,
        title: "Unreachable code",
        messageFormat: "Unreachable code detected",
        category: "Control flow",
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        description: "Detects throw statements that cannot be reached due to surrounding control flow or exception handling.",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RuleUnhandledException, RuleIgnoredException, RuleGeneralThrowDeclared, RuleGeneralThrow, RuleDuplicateDeclarations, RuleMissingThrowsOnBaseMember, RuleMissingThrowsFromBaseMember, RuleDuplicateThrowsByHierarchy, RuleRedundantTypedCatchClause, RuleRedundantCatchAllClause, RuleThrowsDeclarationNotValidOnFullProperty, RuleXmlDocButNoThrows, RuleRedundantExceptionDeclaration, RuleCatchHandlesNoRemainingExceptions, RuleUnreachableCode, RuleUnreachableCodeHidden];

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
        context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.AddAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventAssignment, SyntaxKind.SubtractAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeTryStatement, SyntaxKind.TryStatement);
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeCastExpression, SyntaxKind.CastExpression);
        context.RegisterSyntaxNodeAction(AnalyzeForeachStatement, SyntaxKind.ForEachStatement);
    }

    private void AnalyzeForeachStatement(SyntaxNodeAnalysisContext context)
    {
        var forEachSyntax = (ForEachStatementSyntax)context.Node;

        var settings = GetAnalyzerSettings(context.Options);

        if (!settings.IsLinqSupportEnabled)
            return;

        var semanticModel = context.SemanticModel;

        var op = semanticModel.GetOperation(forEachSyntax);
        if (op is not IForEachLoopOperation forEachOp)
            return;

        // Collect exceptions that will surface when enumeration happens
        var exceptionTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        CollectEnumerationExceptions(forEachOp.Collection, exceptionTypes, semanticModel, context.CancellationToken);

        // Your existing nullability post-processing
        exceptionTypes = new HashSet<INamedTypeSymbol>(
            ProcessNullable(context, forEachSyntax.Expression, null, exceptionTypes),
            SymbolEqualityComparer.Default);

        foreach (var t in exceptionTypes.Distinct(SymbolEqualityComparer.Default))
        {
            AnalyzeExceptionThrowingNode(context, forEachSyntax.Expression, (INamedTypeSymbol?)t, settings);
        }
    }

    private void AnalyzeCastExpression(SyntaxNodeAnalysisContext context)
    {
        var castExpression = (CastExpressionSyntax)context.Node;
        var settings = GetAnalyzerSettings(context.Options);

        var sourceType = context.SemanticModel.GetTypeInfo(castExpression.Expression).Type;
        var targetType = context.SemanticModel.GetTypeInfo(castExpression.Type).Type;

        if (sourceType is null || targetType is null)
            return;

        INamedTypeSymbol? exceptionType = CheckCastExpression(context, castExpression, targetType);

        if (exceptionType is not null)
        {
            AnalyzeExceptionThrowingNode(context, castExpression, exceptionType, settings);
        }
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = context.Node as PropertyDeclarationSyntax;

        var semanticModel = context.SemanticModel;

        if (propertyDeclaration is null)
            return;

        var throwsAttributes = propertyDeclaration.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute");

        var accessorList = propertyDeclaration.AccessorList as AccessorListSyntax;

        if (accessorList is not null)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);

            // Check whether this concrete full property decl has any Throws declarations on it.
            CheckNoThrowsOnFullPropertyDecl(context, throwsAttributes);

            if (accessorList.Accessors.All(x => x.ExpressionBody is null && x.Body is null))
            {
                // Only give diagnostic when we can do anything useful with the docs.

                CheckXmlDocsForUndeclaredExceptions_Property(throwsAttributes, context);
            }
        }
        else if (propertyDeclaration.ExpressionBody is not null)
        {
            var settings = GetAnalyzerSettings(context.Options);

            if (settings.IsControlFlowAnalysisEnabled)
            {
                AnalyzeControlFlow_ExpressionBodiedProperty(throwsAttributes, context);
            }

            CheckXmlDocsForUndeclaredExceptions_ExpressionBodiedProperty(throwsAttributes, context);
            return;
        }
    }

    // Legacy redundancy check
    private void AnalyzeTryStatement(SyntaxNodeAnalysisContext context)
    {
        var tryStatement = context.Node as TryStatementSyntax;

        if (tryStatement is null)
            return;

        var settings = GetAnalyzerSettings(context.Options);

        if (!settings.IsControlFlowAnalysisEnabled && settings.IsLegacyRedundancyChecksEnabled)
        {
            var semanticModel = context.SemanticModel;

            var thrownExceptions = CollectUnhandledExceptions(context, tryStatement.Block, settings);

            HashSet<INamespaceOrTypeSymbol> unhandledExceptions = new HashSet<INamespaceOrTypeSymbol>(thrownExceptions, SymbolEqualityComparer.Default);

            // Check for redundant typed catch clauses
            foreach (var catchClause in tryStatement.Catches)
            {
                if (catchClause.Declaration?.Type is null)
                {
                    if (unhandledExceptions.Count > 0)
                        continue;

                    unhandledExceptions.Clear();

                    // Report redundant catch clause
                    var diagnostic = Diagnostic.Create(
                            RuleRedundantCatchAllClause,
                            catchClause.CatchKeyword.GetLocation());

                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                    if (catchType is null)
                        continue;

                    // Update unhandled set
                    unhandledExceptions.RemoveWhere(thrown =>
                        SymbolEqualityComparer.Default.Equals(thrown, catchType) ||
                        (thrown is INamedTypeSymbol named && named.InheritsFrom(catchType)));

                    // Check if any thrown exception matches or derives from this catch type
                    bool isRelevant = thrownExceptions.OfType<INamedTypeSymbol>().Any(thrown =>
                        thrown.Equals(catchType, SymbolEqualityComparer.Default) ||
                        thrown.InheritsFrom(catchType));

                    if (!isRelevant)
                    {
                        // Report redundant catch clause
                        var diagnostic = Diagnostic.Create(
                            RuleRedundantTypedCatchClause,
                            catchClause.Declaration.Type.GetLocation(),
                            catchType.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    private void AnalyzeMethodSymbol(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol is null)
            return;

        var settings = GetAnalyzerSettings(context.Options);

        var throwsAttributes = GetThrowsAttributes(methodSymbol).ToImmutableArray();

        CheckForCompatibilityWithBaseOrInterface(context, throwsAttributes);

        CheckXmlDocsForUndeclaredExceptions(throwsAttributes, context);

        if (settings.IsControlFlowAnalysisEnabled)
        {
            AnalyzeControlFlow(context, throwsAttributes);
        }

        if (throwsAttributes.Length is 0)
            return;

        CheckForGeneralExceptionThrowDeclarations(throwsAttributes, context);
        CheckForDuplicateThrowsDeclarations(context, throwsAttributes);
        CheckForRedundantThrowsDeclarationsHandledByDeclaredSuperClass(context, throwsAttributes);
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
    /// Analyzes implicit object creation expressions to determine if exceptions are handled or declared.
    /// </summary>
    private void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var objectCreation = (ImplicitObjectCreationExpressionSyntax)context.Node;

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
    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
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
    private static void AnalyzeEventAssignment(SyntaxNodeAnalysisContext context)
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

    private static void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
    {
        var lambdaExpression = (LambdaExpressionSyntax)context.Node;
        AnalyzeFunctionAttributes(lambdaExpression, lambdaExpression.AttributeLists.SelectMany(a => a.Attributes), context.SemanticModel, context);
    }

    private static void AnalyzeLocalFunctionStatement(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        AnalyzeFunctionAttributes(localFunction, localFunction.AttributeLists.SelectMany(a => a.Attributes), context.SemanticModel, context);
    }
}
