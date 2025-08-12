using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// From method symbol
    /// </summary>
    private static void AnalyzeControlFlow(
        SymbolAnalysisContext context,
        ImmutableArray<AttributeData> throwsAttributes)
    {
        if (context.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Skip abstract / extern (no body to analyze)
        if (methodSymbol.IsAbstract || methodSymbol.IsExtern)
            return;

        // Collect all declared exception types from [Throws]
        var declared = GetExceptionTypes(throwsAttributes)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        if (IsAbstractOrVirtualAutoProperty(methodSymbol))
        {
            return;
        }

        // Collect all actually escaping exceptions

        var node = methodSymbol.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).FirstOrDefault();

        var semanticModel = context.Compilation.GetSemanticModel(node.SyntaxTree);

        var actual = CollectThrownExceptions(methodSymbol, context.Compilation, semanticModel, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
               exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(methodSymbol, (INamedTypeSymbol)declaredType!, context.Compilation)
                               ?? methodSymbol.Locations.FirstOrDefault();

                ReportRedundantExceptionDeclaration(context.ReportDiagnostic, declaredType, location);
            }
        }
    }

    private static bool IsAbstractOrVirtualAutoProperty(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.AssociatedSymbol is IPropertySymbol propertySymbol && (propertySymbol.IsAbstract || propertySymbol.IsVirtual))
        {
            foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is AccessorDeclarationSyntax accessorDecl)
                {
                    // Auto-property accessor if both Body and ExpressionBody are null
                    if (accessorDecl.Body is null && accessorDecl.ExpressionBody is null)
                    {
                        return true; // skip analysis
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// For local functions and lambda syntaxes
    /// </summary>
    private static void AnalyzeControlFlow(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var node = context.Node;

        IMethodSymbol? methodSymbol = null;

        if (node is LocalFunctionStatementSyntax)
        {
            methodSymbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
        }
        else if (node is AnonymousFunctionExpressionSyntax)
        {
            methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
        }

        if (methodSymbol is null)
            return;

        // Collect all declared exception types from [Throws]
        var declared = throwsAttributes.SelectMany(x => GetExceptionTypes(x, semanticModel))
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        // Collect all actually escaping exceptions
        var actual = CollectThrownExceptions(methodSymbol, semanticModel.Compilation, semanticModel, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
                exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(methodSymbol, (INamedTypeSymbol?)declaredType!, context.Compilation)
                               ?? node.GetLocation();

                ReportRedundantExceptionDeclaration(context.ReportDiagnostic, declaredType, location);
            }
        }
    }

    private static void AnalyzeControlFlow_ExpressionBodiedProperty(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var node = context.Node;

        IPropertySymbol? propertySymbol = null;

        if (node is PropertyDeclarationSyntax)
        {
            propertySymbol = semanticModel.GetDeclaredSymbol(node) as IPropertySymbol;
        }

        if (propertySymbol is null)
            return;

        // Collect all declared exception types from [Throws]
        var declared = throwsAttributes.SelectMany(x => GetExceptionTypes(x, semanticModel))
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        // Collect all actually escaping exceptions
        var actual = CollectThrownExceptions(node, semanticModel.Compilation, semanticModel, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
                exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(propertySymbol, (INamedTypeSymbol?)declaredType!, context.Compilation)
                               ?? node.GetLocation();

                ReportRedundantExceptionDeclaration(context.ReportDiagnostic, declaredType, location);
            }
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
        IMethodSymbol method,
        Compilation compilation,
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic,
        AnalyzerOptions analyzerOptions)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var syntax = syntaxRef.GetSyntax();

        return CollectThrownExceptions(syntax, compilation, semanticModel, reportDiagnostic, analyzerOptions);
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
         SyntaxNode node,
         Compilation compilation,
         SemanticModel semanticModel,
         Action<Diagnostic> reportDiagnostic,
         AnalyzerOptions analyzerOptions)
    {
        CompilationUnitSyntax? compilationUnit = null;
        BlockSyntax? body = null;
        ExpressionSyntax? expressionBody = null;

        switch (node)
        {
            case CompilationUnitSyntax cu:
                compilationUnit = cu;
                break;

            case BaseMethodDeclarationSyntax methodDecl:
                body = methodDecl.Body;
                expressionBody = methodDecl.ExpressionBody?.Expression;
                break;

            case PropertyDeclarationSyntax propertyDeclaration:
                expressionBody = propertyDeclaration.ExpressionBody?.Expression;
                break;

            case AccessorDeclarationSyntax accessorDeclaration:
                body = accessorDeclaration.Body;
                expressionBody = accessorDeclaration.ExpressionBody?.Expression;
                break;

            case LocalFunctionStatementSyntax localFunction:
                body = localFunction.Body;
                expressionBody = localFunction.ExpressionBody?.Expression;
                break;

            case AnonymousFunctionExpressionSyntax anonymousFunction:
                body = anonymousFunction.Block;
                // Lambdas can be block‑bodied or expression‑bodied
                if (body is null && anonymousFunction.ExpressionBody is ExpressionSyntax expr)
                {
                    expressionBody = expr;
                }
                break;
        }

        var context = new SyntaxNodeAnalysisContext(
            node,
            semanticModel,
            options: analyzerOptions,
            reportDiagnostic: reportDiagnostic,
            isSupportedDiagnostic: _ => true,
            cancellationToken: default);

        var settings = GetAnalyzerSettings(context.Options);

        if (compilationUnit is not null)
        {
            var unhandled = AnalyzeBlockWithExceptions(new ControlFlowContext(context, compilationUnit, settings));
            return [.. unhandled.UnhandledExceptions.OfType<INamedTypeSymbol>()];
        }
        else if (body is not null)
        {
            var unhandled = AnalyzeBlockWithExceptions(new ControlFlowContext(context, body, settings));
            return [.. unhandled.UnhandledExceptions.OfType<INamedTypeSymbol>()];
        }
        else if (expressionBody is not null)
        {
            // Collect exceptions directly from the expression
            var exceptions = CollectExceptionsFromExpression(context, expressionBody, settings, semanticModel);
            return [.. exceptions.OfType<INamedTypeSymbol>()];
        }

        return [];
    }

    public class ControlFlowContext
    {
        public ControlFlowContext(
            SyntaxNodeAnalysisContext syntaxContext,
            SyntaxNode node,
            AnalyzerSettings settings,
            HashSet<INamedTypeSymbol>? triedExceptions = null,
            HashSet<INamedTypeSymbol>? remainingExceptions = null,
            HashSet<INamedTypeSymbol>? previouslyCaughtExceptionTypes = null,
            bool isUnreachable = false)
        {
            SyntaxContext = syntaxContext;
            Node = node;
            Settings = settings;
            TriedExceptions = triedExceptions ?? [];
            RemainingExceptions = remainingExceptions;
            PreviouslyCaughtExceptionTypes = previouslyCaughtExceptionTypes ?? [];
            IsUnreachable = isUnreachable;
        }

        public ControlFlowContext(
          ControlFlowContext parentContext,
          SyntaxNode node,
          HashSet<INamedTypeSymbol>? triedExceptions = null,
          HashSet<INamedTypeSymbol>? remainingExceptions = null,
          HashSet<INamedTypeSymbol>? previouslyCaughtExceptionTypes = null,
          bool isUnreachable = false)
          : this(parentContext.SyntaxContext, node, parentContext.Settings, triedExceptions, remainingExceptions, previouslyCaughtExceptionTypes, isUnreachable)
        {
            Parent = parentContext;
        }

        public ControlFlowContext? Parent { get; }

        public Compilation Compilation => SyntaxContext.Compilation;
        public SemanticModel SemanticModel => SyntaxContext.SemanticModel;

        public SyntaxNodeAnalysisContext SyntaxContext { get; }
        public SyntaxNode Node { get; }
        public AnalyzerSettings Settings { get; }

        /// <summary>
        /// Exceptions identified in immediate Try block
        /// </summary>
        public HashSet<INamedTypeSymbol>? TriedExceptions { get; }

        /// <summary>
        /// Remaining unhandled exceptions from try block
        /// </summary>
        /// <remarks>Remove exception to handle.</remarks>
        public HashSet<INamedTypeSymbol>? RemainingExceptions { get; }

        /// <summary>
        /// Previously caught exception types (if from catch)
        /// </summary>
        /// <remarks>Remove exception to handle.</remarks>
        public HashSet<INamedTypeSymbol> PreviouslyCaughtExceptionTypes { get; }

        /// <summary>
        /// Indicates that the block is unreachable
        /// </summary>
        public bool IsUnreachable { get; }

        public void ReportRedundantCatchAll(CatchClauseSyntax catchClause)
        {
            ReportRedundantCatchClause(SyntaxContext, catchClause);

            SyntaxContext.ReportDiagnostic(Diagnostic.Create(
                RuleRedundantCatchAllClause,
                catchClause.CatchKeyword.GetLocation()));
        }

        public void ReportUnreachableCode(SyntaxNode node)
        {
            ReportUnreachableCode(node.GetLocation());
        }

        public void ReportUnreachableCode(Location location)
        {
            SyntaxContext.ReportDiagnostic(Diagnostic.Create(
                RuleUnreachableCode,
                location));
        }

        public void ReportUnreachableCodeHidden(Location location)
        {
            // Prevents the diagnostic from being reported in a nested block.
            if (IsUnreachable)
                return;

            SyntaxContext.ReportDiagnostic(Diagnostic.Create(
                RuleUnreachableCodeHidden,
                location));
        }
    }

    private static FlowWithExceptionsResult AnalyzeBlockWithExceptions(
      ControlFlowContext context)
    {
        var semanticModel = context.SemanticModel;

        var unhandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        bool reachable = true;

        var node = context.Node;

        IEnumerable<StatementSyntax>? statements = null;

        if (node is BlockSyntax block)
        {
            statements = block.Statements;
        }
        else if (node is CompilationUnitSyntax cu)
        {
            statements = cu.Members
                .OfType<GlobalStatementSyntax>()
                .Select(x => x.Statement);
        }
        else
            throw new Exception($"Unsupported node type: {node.GetType()}");

        bool containsReturn = false;

        foreach (var statement in statements)
        {
            if (!reachable)
            {
                if (statement is not LocalFunctionStatementSyntax)
                {
                    context.ReportUnreachableCode(statement);
                }
                ReportUnreachableCodeHidden(context, statements, statement);
                break;
            }

            // Delegate analysis to the per‑statement helper
            var stmtResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, statement, context.TriedExceptions, context.RemainingExceptions, isUnreachable: context.IsUnreachable));

            if (stmtResult.ContainsReturn)
                containsReturn = true;

            // Merge exceptions
            unhandled.UnionWith(stmtResult.UnhandledExceptions);

            // Update reachability
            reachable = stmtResult.EndReachable;
        }

        return new FlowWithExceptionsResult(
            node,
            reachable,
            containsReturn,
            unhandled.ToImmutableHashSet(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>().ToImmutableHashSet());
    }

    private static void ReportRedundantExceptionDeclaration(Action<Diagnostic> reportDiagnostic, ISymbol? declaredType, Location? location)
    {
        var diagnostic = Diagnostic.Create(
            RuleRedundantExceptionDeclaration,
            location,
            declaredType.Name);

        reportDiagnostic(diagnostic);
    }

    private static void ReportRedundantTypedCatchClause(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, INamedTypeSymbol caughtType)
    {
        ReportRedundantCatchClause(context, catchClause);

        context.ReportDiagnostic(Diagnostic.Create(
            RuleRedundantTypedCatchClause,
            catchClause.Declaration.Type.GetLocation(),
            caughtType.Name));
    }

    private static void ReportRedundantCatchClause(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleRedundantCatchClause,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static void ReportCatchHandlesNoRemainingExceptions(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, IEnumerable<INamedTypeSymbol> exceptionTypes)
    {
        ReportRedundantCatchClause(context, catchClause);

        var exceptionTypeNames = exceptionTypes.Select(x => $"'{x.Name}'");

        context.ReportDiagnostic(Diagnostic.Create(
            RuleCatchHandlesNoRemainingExceptions,
            catchClause.Declaration.Type.GetLocation(),
            string.Join(", ", exceptionTypeNames)));
    }

    private static void ReportUnreachableCodeHidden(ControlFlowContext context, IEnumerable<StatementSyntax> statements, StatementSyntax statement)
    {
        var statementIndex = statements.TakeWhile(x => x != statement).Count();

        // 🚩 We already know the block can’t continue past here
        for (int i = statementIndex; i < statements.Count(); i++)
        {
            var s = statements.ElementAt(i);

            // Start of an unreachable region
            int start = i;

            // Advance until we hit the end of the block, or something that resets analysis
            while (i < statements.Count())
            {
                var current = statements.ElementAt(i);

                // Local functions are *not* unreachable code
                if (current is LocalFunctionStatementSyntax)
                    break;

                // Extend the unreachable block
                i++;
            }

            int end = i - 1; // last unreachable statement before break
            if (end >= start)
            {
                var span = TextSpan.FromBounds(
                    statements.ElementAt(start).FullSpan.Start,
                    statements.ElementAt(end).FullSpan.End);

                var location = Location.Create(context.Node.SyntaxTree, span);
                context.ReportUnreachableCodeHidden(location);
            }

            // Continue outer loop — i already points at first reachable again
            continue;
        }
    }

    private static FlowWithExceptionsResult AnalyzeStatementWithExceptions(
        ControlFlowContext context)
    {
        var semanticModel = context.SemanticModel;

        var statement = (StatementSyntax)context.Node;

        // Handle nested blocks
        if (statement is BlockSyntax block)
        {
            return AnalyzeBlockWithExceptions(new ControlFlowContext(context, block));
        }

        var unhandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        switch (statement)
        {
            case ThrowStatementSyntax throwStmt:
                if (throwStmt.Expression is null)
                {
                    // 🚩 Rethrow
                    if (context.RemainingExceptions is not null)
                        unhandled.AddRange(context.RemainingExceptions);
                }
                else
                {
                    // Normal throw
                    unhandled.UnionWith(CollectExceptionsFromStatement(context.SyntaxContext, statement, context.Settings));
                }
                return new FlowWithExceptionsResult(throwStmt, false, unhandled.ToImmutableHashSet());

            case TryStatementSyntax tryStmt:
                return AnalyzeTryStatement(new ControlFlowContext(context, tryStmt, unhandled, context.RemainingExceptions, isUnreachable: context.IsUnreachable));

            case IfStatementSyntax ifStmt:
                {
                    // exceptions in condition
                    unhandled.UnionWith(CollectExceptionsFromExpression(
                        context.SyntaxContext, ifStmt.Condition, context.Settings, semanticModel));

                    // Try constant-fold the condition
                    var constantValue = semanticModel.GetConstantValue(ifStmt.Condition);

                    if (constantValue.HasValue && constantValue.Value is bool constBool)
                    {
                        // 🚩 Constant condition — only one branch can ever run
                        if (constBool)
                        {
                            // Always true → analyze THEN only
                            var thenResult2 = AnalyzeStatementWithExceptions(
                                new ControlFlowContext(context, ifStmt.Statement));
                            unhandled.UnionWith(thenResult2.UnhandledExceptions);

                            return new FlowWithExceptionsResult(
                                ifStmt,
                                thenResult2.EndReachable,
                                unhandled.ToImmutableHashSet());
                        }
                        else
                        {
                            // Always false → analyze ELSE only
                            if (ifStmt.Else?.Statement is not null)
                            {
                                var elseResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, ifStmt.Else.Statement));
                                unhandled.UnionWith(elseResult.UnhandledExceptions);

                                return new FlowWithExceptionsResult(
                                    ifStmt,
                                    elseResult.EndReachable,
                                    unhandled.ToImmutableHashSet());
                            }

                            // No else → condition false means it just falls through
                            return new FlowWithExceptionsResult(ifStmt, true, unhandled.ToImmutableHashSet());
                        }
                    }

                    // 🚩 Normal branching if (x) — both branches possible
                    bool continuation = true; // false

                    // Then branch
                    FlowWithExceptionsResult? thenResult = null;
                    if (ifStmt.Statement is not null)
                    {
                        thenResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, ifStmt.Statement));
                        unhandled.UnionWith(thenResult.UnhandledExceptions);

                        if (thenResult.EndReachable)
                            continuation = true;
                    }

                    // Else branch
                    if (ifStmt.Else?.Statement is not null)
                    {
                        var elseResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, ifStmt.Else.Statement));
                        unhandled.UnionWith(elseResult.UnhandledExceptions);

                        continuation = elseResult.EndReachable;
                    }
                    else
                    {
                        // Only continues if the THEN branch doesn’t always terminate
                        if (thenResult?.EndReachable ?? false)
                            continuation = true;
                    }

                    return new FlowWithExceptionsResult(ifStmt, continuation, unhandled.ToImmutableHashSet());
                }

            case WhileStatementSyntax whileStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, whileStmt.Condition, context.Settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, whileStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    // Try constant-fold the condition
                    var constantValue = semanticModel.GetConstantValue(whileStmt.Condition);

                    if (constantValue.HasValue && constantValue.Value is bool constBool)
                    {
                        // 🚩 Constant condition — only one branch can ever run
                        if (constBool)
                        {
                            // Always true → analyze THEN only
                            var thenResult2 = AnalyzeStatementWithExceptions(new ControlFlowContext(context, whileStmt.Statement));
                            unhandled.UnionWith(thenResult2.UnhandledExceptions);

                            return new FlowWithExceptionsResult(
                                whileStmt,
                                thenResult2.EndReachable,
                                unhandled.ToImmutableHashSet());
                        }
                        else
                        {
                            // No else → condition false means it just falls through
                            return new FlowWithExceptionsResult(whileStmt, true, unhandled.ToImmutableHashSet());
                        }
                    }

                    bool continuation = true;

                    if (!bodyResult.EndReachable)
                    {
                        continuation = false;
                    }

                    // otherwise assume reachable
                    return new FlowWithExceptionsResult(whileStmt, continuation, unhandled.ToImmutableHashSet());
                }

            case DoStatementSyntax doStmt:
                {
                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, doStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, doStmt.Condition, context.Settings, semanticModel));

                    return new FlowWithExceptionsResult(doStmt, true, unhandled.ToImmutableHashSet());
                }

            case ForStatementSyntax forStmt:
                {
                    foreach (var init in forStmt.Initializers)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, init, context.Settings, semanticModel));

                    if (forStmt.Condition is not null)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, forStmt.Condition, context.Settings, semanticModel));

                    foreach (var inc in forStmt.Incrementors)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, inc, context.Settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, forStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(forStmt, true, unhandled.ToImmutableHashSet());
                }

            case ForEachStatementSyntax foreachStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, foreachStmt.Expression, context.Settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, foreachStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(foreachStmt, true, unhandled.ToImmutableHashSet());
                }

            case SwitchStatementSyntax switchStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, switchStmt.Expression, context.Settings, semanticModel));

                    bool continuation = false;
                    foreach (var section in switchStmt.Sections)
                    {
                        foreach (var label in section.Labels)
                        {
                            // labels themselves don’t throw
                        }

                        foreach (var st in section.Statements)
                        {
                            var stResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, st));
                            unhandled.UnionWith(stResult.UnhandledExceptions);

                            if (stResult.EndReachable)
                                continuation = true;
                        }
                    }

                    return new FlowWithExceptionsResult(switchStmt, continuation, unhandled.ToImmutableHashSet());
                }

            case UsingStatementSyntax usingStmt:
                {
                    if (usingStmt.Expression is not null)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, usingStmt.Expression, context.Settings, semanticModel));

                    if (usingStmt.Declaration is not null)
                    {
                        foreach (var v in usingStmt.Declaration.Variables)
                        {
                            if (v.Initializer?.Value is not null)
                                unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, v.Initializer.Value, context.Settings, semanticModel));
                        }
                    }

                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, usingStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(usingStmt, bodyResult.EndReachable, unhandled.ToImmutableHashSet());
                }

            case LockStatementSyntax lockStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, lockStmt.Expression, context.Settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(new ControlFlowContext(context, lockStmt.Statement));
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(lockStmt, bodyResult.EndReachable, unhandled.ToImmutableHashSet());
                }

            case BreakStatementSyntax:
            case ContinueStatementSyntax:
                {
                    // Terminates current block
                    return new FlowWithExceptionsResult(statement, false, unhandled.ToImmutableHashSet());
                }

            case ReturnStatementSyntax returnStmt:
                {
                    if (returnStmt.Expression is not null)
                    {
                        unhandled.UnionWith(CollectExceptionsFromExpression(context.SyntaxContext, returnStmt.Expression, context.Settings, semanticModel));
                    }
                    return new FlowWithExceptionsResult(returnStmt, endReachable: false, containsReturn: true, unhandledExceptions: unhandled.ToImmutableHashSet());
                }

            case LocalDeclarationStatementSyntax localDecl
    when localDecl.Declaration.Variables
        .Any(v => v.Initializer?.Value is InvocationExpressionSyntax
               or ElementAccessExpressionSyntax
               or ObjectCreationExpressionSyntax
               or ImplicitObjectCreationExpressionSyntax):
                unhandled.UnionWith(CollectExceptionsFromStatement(context.SyntaxContext, statement, context.Settings));
                return new FlowWithExceptionsResult(localDecl, true, unhandled.ToImmutableHashSet());

            case ExpressionStatementSyntax exprStmt
    when exprStmt.Expression.DescendantNodesAndSelf()
        .Any(n => n is InvocationExpressionSyntax
               or ElementAccessExpressionSyntax
               or ObjectCreationExpressionSyntax
               or ImplicitObjectCreationExpressionSyntax):
                unhandled.UnionWith(CollectExceptionsFromStatement(context.SyntaxContext, statement, context.Settings));
                return new FlowWithExceptionsResult(exprStmt, true, unhandled.ToImmutableHashSet());

            default:
                var flow = semanticModel.AnalyzeControlFlow(statement);

                unhandled.UnionWith(CollectExceptionsFromStatement(context.SyntaxContext, statement, context.Settings));

                // Fallback: assume it falls through
                return new FlowWithExceptionsResult(
                    statement,
                    flow.Succeeded ? flow.EndPointIsReachable : true,
                    unhandled.ToImmutableHashSet());
        }
    }

    private static FlowWithExceptionsResult AnalyzeTryStatement(ControlFlowContext context)
    {
        var tryStmt = (TryStatementSyntax)context.Node;
        var triedExceptions = context.TriedExceptions ?? [];

        // === Analyze try body ===
        var tryResult = AnalyzeBlockWithExceptions(new ControlFlowContext(context, tryStmt.Block, remainingExceptions: context.RemainingExceptions));
        triedExceptions.UnionWith(tryResult.UnhandledExceptions);

        bool continuationPossible = tryResult.EndReachable;
        bool containsReturn = tryResult.ContainsReturn;

        var exceptionsLeftToHandle = new HashSet<INamedTypeSymbol>(tryResult.UnhandledExceptions, SymbolEqualityComparer.Default);

        var previouslyCaughtExceptionTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // === Analyze each catch ===
        foreach (var catchClause in tryStmt.Catches)
        {
            var catchResult = AnalyzeCatchClause(
                new ControlFlowContext(
                       context, catchClause, triedExceptions, exceptionsLeftToHandle, [.. previouslyCaughtExceptionTypes], isUnreachable: context.IsUnreachable),
                       new HashSet<INamedTypeSymbol>(tryResult.UnhandledExceptions, SymbolEqualityComparer.Default),
                       exceptionsLeftToHandle);

            if (catchResult.CaughtExceptionType is not null)
                previouslyCaughtExceptionTypes.Add(catchResult.CaughtExceptionType);

            continuationPossible |= catchResult.EndReachable;
            containsReturn |= catchResult.ContainsReturn;

            triedExceptions.UnionWith(catchResult.UnhandledExceptions);
        }

        // === Analyze finally ===
        if (tryStmt.Finally?.Block is { } finallyBlock)
        {
            var finallyResult = AnalyzeBlockWithExceptions(new ControlFlowContext(context, finallyBlock));

            // Always merge exceptions
            triedExceptions.UnionWith(finallyResult.UnhandledExceptions);

            if (!finallyResult.EndReachable && !finallyResult.ContainsReturn)
            {
                // Finally always terminates
                triedExceptions.Clear();
                triedExceptions.UnionWith(finallyResult.UnhandledExceptions);
                continuationPossible = false;
            }
            else
            {
                continuationPossible = continuationPossible && finallyResult.EndReachable;
                containsReturn |= finallyResult.ContainsReturn;
            }
        }

        return new FlowWithExceptionsResult(
            tryStmt,
            continuationPossible,
            containsReturn,
            triedExceptions.ToImmutableHashSet());
    }

    private static FlowWithExceptionsResult AnalyzeCatchClause(
        ControlFlowContext context,
        IReadOnlyCollection<INamedTypeSymbol> exceptionsInTry,
        HashSet<INamedTypeSymbol> exceptionsLeftToHandle)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        var triedExceptions = context.TriedExceptions ?? [];

        bool continuationPossible = false;
        bool containsReturn = false;

        INamedTypeSymbol? caughtExceptionType = null;

        // --- catch-all ---
        if (catchClause.Declaration?.Type is null)
        {
            bool handlesAny = exceptionsLeftToHandle.Count > 0;
            if (!handlesAny)
                context.ReportRedundantCatchAll(catchClause);

            var caughtExceptionsInCatch = new HashSet<INamedTypeSymbol>(exceptionsLeftToHandle);

            // Swallow everything the try might throw
            triedExceptions.RemoveWhere(ex => exceptionsLeftToHandle.Contains(ex));

            if (catchClause.Block is { } catchBlock)
            {
                var catchResult = AnalyzeBlockWithExceptions(
                    new ControlFlowContext(
                        context,
                        catchBlock,
                        caughtExceptionsInCatch,
                        new HashSet<INamedTypeSymbol>(exceptionsLeftToHandle, SymbolEqualityComparer.Default),
                        isUnreachable: !handlesAny));

                triedExceptions.UnionWith(catchResult.UnhandledExceptions);

                if (handlesAny && catchResult.EndReachable)
                    continuationPossible = true;

                containsReturn |= catchResult.ContainsReturn;

                if (!handlesAny)
                    context.ReportUnreachableCodeHidden(catchClause.GetLocation());
            }

            exceptionsLeftToHandle.Clear();
        }
        else
        {
            // --- typed catch ---
            caughtExceptionType = GetCaughtException(catchClause, context.SemanticModel);
            bool handlesAny = false;

            if (caughtExceptionType is not null)
            {
                // Is this type ever thrown in the try?
                handlesAny = exceptionsInTry.Any(ex => IsExceptionCaught(ex, caughtExceptionType));

                if (!handlesAny)
                {
                    // Case 1: Never thrown at all → redundant
                    ReportRedundantTypedCatchClause(context.SyntaxContext, catchClause, caughtExceptionType);

                    context.ReportUnreachableCodeHidden(catchClause.GetLocation());
                }
                else
                {
                    // Case 2 + 3: Could be thrown, but has it already been handled?
                    bool alreadyHandledByEarlier = !exceptionsLeftToHandle.Any(ex => IsExceptionCaught(ex, caughtExceptionType));

                    if (alreadyHandledByEarlier)
                    {
                        // 🔎 Distinguish overshadowed vs no-remaining-exceptions
                        if (!IsOvershadowedByEarlier(caughtExceptionType, context))
                        {
                            var previouslyCaughtMatchingExceptions = context.PreviouslyCaughtExceptionTypes
                                .Where(ex => IsExceptionCaught(ex, caughtExceptionType));

                            // Case 3: broad catch after all specifics already covered
                            ReportCatchHandlesNoRemainingExceptions(context.SyntaxContext, catchClause, previouslyCaughtMatchingExceptions);
                        }
                        else
                        {
                            // INFO: Overshadowing is already reported by C# as CS0160

                            ReportRedundantCatchClause(context.SyntaxContext, catchClause);
                        }

                        // This catch clause doesn't handle anything. Exclude from analysis.
                        handlesAny = false;

                        context.ReportUnreachableCodeHidden(catchClause.GetLocation());
                    }
                }

                if (handlesAny)
                {
                    triedExceptions.RemoveWhere(ex => IsExceptionCaught(ex, caughtExceptionType));
                    exceptionsLeftToHandle.RemoveWhere(ex => IsExceptionCaught(ex, caughtExceptionType));
                }
            }

            if (catchClause.Block is { } catchBlock)
            {
                var catchResult = AnalyzeBlockWithExceptions(
                    new ControlFlowContext(
                    context,
                    catchBlock,
                    caughtExceptionType is not null ? [caughtExceptionType] : null,
                    isUnreachable: !handlesAny));

                if (handlesAny)
                    triedExceptions.UnionWith(catchResult.UnhandledExceptions);

                if (handlesAny && catchResult.EndReachable)
                    continuationPossible = true;

                containsReturn |= catchResult.ContainsReturn;
            }
        }

        return new FlowWithExceptionsResult(
            catchClause,
            continuationPossible,
            containsReturn,
            unhandledExceptions: triedExceptions.ToImmutableHashSet(),
            caughtExceptionType: caughtExceptionType is not null ? caughtExceptionType : context.Compilation.GetTypeByMetadataName("System.Exception"));
    }

    private static bool IsOvershadowedByEarlier(INamedTypeSymbol caught, ControlFlowContext context)
    {
        foreach (var prev in context.PreviouslyCaughtExceptionTypes)
        {
            // If the current catch type (caught) is assignable to a previous type (prev),
            // then it is overshadowed.
            if (IsExceptionCaught(caught, prev))
                return true;
        }
        return false;
    }

    private static Location? GetThrowsAttributeLocation(
      ISymbol symbol,
      INamedTypeSymbol declaredType,
      Compilation compilation)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            // Match purely on attribute name (ignores namespace)
            if (!string.Equals(attr.AttributeClass?.Name, "ThrowsAttribute", StringComparison.Ordinal))
                continue;

            if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax attrSyntax)
                continue;

            var semanticModel = compilation.GetSemanticModel(attrSyntax.SyntaxTree);

            foreach (var arg in attrSyntax.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>())
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;

                    // Primary: semantic match
                    if (declaredType.Equals(typeInfo, SymbolEqualityComparer.Default))
                        return typeOfExpr.Type.GetLocation();

                    // Fallback: match on simple name
                    var writtenName = typeOfExpr.Type.ToString();
                    if (declaredType.Name.Equals(writtenName, StringComparison.Ordinal) ||
                        declaredType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            .Equals(writtenName, StringComparison.Ordinal))
                    {
                        return typeOfExpr.Type.GetLocation();
                    }
                }
            }
        }

        return null;
    }
}

sealed class FlowWithExceptionsResult
{
    /// <summary>
    /// The recently analyzed node
    /// </summary>
    /// <value></value>
    public SyntaxNode Node { get; }

    // TODO: In a future version, include the nodes where there was a return.

    /// <summary>
    /// End is not reachable
    /// </summary>
    /// <value></value>
    public bool EndReachable { get; }

    /// <summary>
    /// A return has been made
    /// </summary>
    /// <value></value>
    public bool ContainsReturn { get; } // or more general: HasNormalTermination

    /// <summary>
    /// The unhandled exceptions propagated by the block. If it's a catch clause, these are exception types remaining.
    /// </summary>
    public ImmutableHashSet<INamedTypeSymbol> UnhandledExceptions { get; }

    /// <summary>
    /// Exception type caught by a catch clause
    /// </summary>
    public INamedTypeSymbol? CaughtExceptionType { get; }

    public FlowWithExceptionsResult(
        SyntaxNode node,
        bool endReachable,
        bool containsReturn,
        ImmutableHashSet<INamedTypeSymbol> unhandledExceptions,
        INamedTypeSymbol? caughtExceptionType = null)
    {
        Node = node;
        EndReachable = endReachable;
        ContainsReturn = containsReturn;
        UnhandledExceptions = unhandledExceptions;
        CaughtExceptionType = caughtExceptionType;
    }

    public FlowWithExceptionsResult(
        SyntaxNode node,
        bool endReachable,
        ImmutableHashSet<INamedTypeSymbol> unhandledExceptions,
        INamedTypeSymbol? caughtExceptionType = null)
    {
        EndReachable = endReachable;
        ContainsReturn = false;
        UnhandledExceptions = unhandledExceptions;
        CaughtExceptionType = caughtExceptionType;
    }

    public static FlowWithExceptionsResult Unreachable(SyntaxNode node) =>
        new(node, false, false, ImmutableHashSet<INamedTypeSymbol>.Empty);
}