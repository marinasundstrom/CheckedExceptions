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
    private void AnalyzeControlFlow(
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
        var actual = CollectThrownExceptions(methodSymbol, context.Compilation, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
               exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(methodSymbol, (INamedTypeSymbol)declaredType!, context.Compilation)
                               ?? methodSymbol.Locations.FirstOrDefault();

                var diagnostic = Diagnostic.Create(
                    RuleRedundantExceptionDeclaration,
                    location,
                    declaredType.Name);

                context.ReportDiagnostic(diagnostic);
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
    private void AnalyzeControlFlow(
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
        var actual = CollectThrownExceptions(methodSymbol, semanticModel.Compilation, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
                exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(methodSymbol, (INamedTypeSymbol?)declaredType!, context.Compilation)
                               ?? node.GetLocation();

                var diagnostic = Diagnostic.Create(
                    RuleRedundantExceptionDeclaration,
                    location,
                    declaredType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private void AnalyzeControlFlow_ExpressionBodiedProperty(
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
        var actual = CollectThrownExceptions(node, semanticModel.Compilation, context.ReportDiagnostic, context.Options);

        // declared - actual = redundant
        foreach (var declaredType in declared)
        {
            if (!actual.Any(exceptionType =>
                exceptionType.IsAssignableTo((ITypeSymbol)declaredType!, context.Compilation)))
            {
                // Try to locate the corresponding attribute syntax for precise squiggle
                var location = GetThrowsAttributeLocation(propertySymbol, (INamedTypeSymbol?)declaredType!, context.Compilation)
                               ?? node.GetLocation();

                var diagnostic = Diagnostic.Create(
                    RuleRedundantExceptionDeclaration,
                    location,
                    declaredType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
        IMethodSymbol method,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        AnalyzerOptions analyzerOptions)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var syntax = syntaxRef.GetSyntax();

        return CollectThrownExceptions(syntax, compilation, reportDiagnostic, analyzerOptions);
    }

    private ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
         SyntaxNode node,
         Compilation compilation,
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

        var semanticModel = compilation.GetSemanticModel(node.SyntaxTree);

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
            var unhandled = AnalyzeBlockWithExceptions(context, compilationUnit, settings);
            return [.. unhandled.UnhandledExceptions.OfType<INamedTypeSymbol>()];
        }
        else if (body is not null)
        {
            var unhandled = AnalyzeBlockWithExceptions(context, body, settings);
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

    private FlowWithExceptionsResult AnalyzeBlockWithExceptions(
      SyntaxNodeAnalysisContext context,
      SyntaxNode node,
      AnalyzerSettings settings,
      HashSet<INamedTypeSymbol>? caughtExceptions = null)
    {
        var semanticModel = context.SemanticModel;

        var unhandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        bool reachable = true;

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
                    ReportUnreachableCode(context, statement);
                }
                ReportUnreachableCodeHidden(context, node, reachable, statements, statement);
                break;
            }

            // Delegate analysis to the per‑statement helper
            var stmtResult = AnalyzeStatementWithExceptions(context, statement, settings, caughtExceptions);

            if (stmtResult.ContainsReturn)
                containsReturn = true;

            // Merge exceptions
            unhandled.UnionWith(stmtResult.UnhandledExceptions);

            // Update reachability
            reachable = stmtResult.EndReachable;
        }

        return new FlowWithExceptionsResult(
            reachable,
            containsReturn,
            unhandled.ToImmutableHashSet(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>().ToImmutableHashSet());
    }

    private void ReportUnreachableCodeHidden(SyntaxNodeAnalysisContext context, SyntaxNode node, bool reachable, IEnumerable<StatementSyntax> statements, StatementSyntax statement)
    {
        var statementIndex = statements.TakeWhile(x => x != statement).Count();

        // 🚩 We already know the block can’t continue past here
        for (int i = statementIndex; i < statements.Count(); i++)
        {
            var s = statements.ElementAt(i);

            if (!reachable)
            {
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

                    var location = Location.Create(node.SyntaxTree, span);
                    ReportUnreachableCodeHidden(context, location);
                }

                // Continue outer loop — i already points at first reachable again
                continue;
            }
        }
        //
    }

    private FlowWithExceptionsResult AnalyzeStatementWithExceptions(
        SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        AnalyzerSettings settings,
        HashSet<INamedTypeSymbol>? caughtExceptions = null)
    {
        var semanticModel = context.SemanticModel;

        // Handle nested blocks
        if (statement is BlockSyntax block)
        {
            return AnalyzeBlockWithExceptions(context, block, settings);
        }

        var unhandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        switch (statement)
        {
            case ThrowStatementSyntax throwStmt:
                if (throwStmt.Expression == null)
                {
                    // 🚩 Rethrow
                    if (caughtExceptions != null)
                        unhandled.AddRange(caughtExceptions);
                }
                else
                {
                    // Normal throw
                    unhandled.UnionWith(CollectExceptionsFromStatement(context, statement, settings));
                }
                return new FlowWithExceptionsResult(false, unhandled.ToImmutableHashSet());

            case TryStatementSyntax tryStmt:
                return AnalyzeTryStatement(context, settings, semanticModel, unhandled, tryStmt);

            case IfStatementSyntax ifStmt:
                {
                    // exceptions in condition
                    unhandled.UnionWith(CollectExceptionsFromExpression(
                        context, ifStmt.Condition, settings, semanticModel));

                    // Try constant-fold the condition
                    var constantValue = semanticModel.GetConstantValue(ifStmt.Condition);

                    if (constantValue.HasValue && constantValue.Value is bool constBool)
                    {
                        // 🚩 Constant condition — only one branch can ever run
                        if (constBool)
                        {
                            // Always true → analyze THEN only
                            var thenResult2 = AnalyzeStatementWithExceptions(context, ifStmt.Statement, settings);
                            unhandled.UnionWith(thenResult2.UnhandledExceptions);

                            return new FlowWithExceptionsResult(
                                thenResult2.EndReachable,
                                unhandled.ToImmutableHashSet());
                        }
                        else
                        {
                            // Always false → analyze ELSE only
                            if (ifStmt.Else?.Statement != null)
                            {
                                var elseResult = AnalyzeStatementWithExceptions(context, ifStmt.Else.Statement, settings);
                                unhandled.UnionWith(elseResult.UnhandledExceptions);

                                return new FlowWithExceptionsResult(
                                    elseResult.EndReachable,
                                    unhandled.ToImmutableHashSet());
                            }

                            // No else → condition false means it just falls through
                            return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());
                        }
                    }

                    // 🚩 Normal branching if (x) — both branches possible
                    bool continuation = true; // false

                    // Then branch
                    FlowWithExceptionsResult? thenResult = null;
                    if (ifStmt.Statement != null)
                    {
                        thenResult = AnalyzeStatementWithExceptions(context, ifStmt.Statement, settings);
                        unhandled.UnionWith(thenResult.UnhandledExceptions);

                        if (thenResult.EndReachable)
                            continuation = true;
                    }

                    // Else branch
                    if (ifStmt.Else?.Statement != null)
                    {
                        var elseResult = AnalyzeStatementWithExceptions(context, ifStmt.Else.Statement, settings);
                        unhandled.UnionWith(elseResult.UnhandledExceptions);

                        continuation = elseResult.EndReachable;
                    }
                    else
                    {
                        // Only continues if the THEN branch doesn’t always terminate
                        if (thenResult?.EndReachable ?? false)
                            continuation = true;
                    }

                    return new FlowWithExceptionsResult(continuation, unhandled.ToImmutableHashSet());
                }

            case WhileStatementSyntax whileStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context, whileStmt.Condition, settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(context, whileStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    // Try constant-fold the condition
                    var constantValue = semanticModel.GetConstantValue(whileStmt.Condition);

                    if (constantValue.HasValue && constantValue.Value is bool constBool)
                    {
                        // 🚩 Constant condition — only one branch can ever run
                        if (constBool)
                        {
                            // Always true → analyze THEN only
                            var thenResult2 = AnalyzeStatementWithExceptions(context, whileStmt.Statement, settings);
                            unhandled.UnionWith(thenResult2.UnhandledExceptions);

                            return new FlowWithExceptionsResult(
                                thenResult2.EndReachable,
                                unhandled.ToImmutableHashSet());
                        }
                        else
                        {
                            // No else → condition false means it just falls through
                            return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());
                        }
                    }

                    bool continuation = true;

                    if (!bodyResult.EndReachable)
                    {
                        continuation = false;
                    }

                    // otherwise assume reachable
                    return new FlowWithExceptionsResult(continuation, unhandled.ToImmutableHashSet());
                }

            case DoStatementSyntax doStmt:
                {
                    var bodyResult = AnalyzeStatementWithExceptions(context, doStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    unhandled.UnionWith(CollectExceptionsFromExpression(context, doStmt.Condition, settings, semanticModel));

                    return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());
                }

            case ForStatementSyntax forStmt:
                {
                    foreach (var init in forStmt.Initializers)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context, init, settings, semanticModel));

                    if (forStmt.Condition != null)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context, forStmt.Condition, settings, semanticModel));

                    foreach (var inc in forStmt.Incrementors)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context, inc, settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(context, forStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());
                }

            case ForEachStatementSyntax foreachStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context, foreachStmt.Expression, settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(context, foreachStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());
                }

            case SwitchStatementSyntax switchStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context, switchStmt.Expression, settings, semanticModel));

                    bool continuation = false;
                    foreach (var section in switchStmt.Sections)
                    {
                        foreach (var label in section.Labels)
                        {
                            // labels themselves don’t throw
                        }

                        foreach (var st in section.Statements)
                        {
                            var stResult = AnalyzeStatementWithExceptions(context, st, settings);
                            unhandled.UnionWith(stResult.UnhandledExceptions);

                            if (stResult.EndReachable)
                                continuation = true;
                        }
                    }

                    return new FlowWithExceptionsResult(continuation, unhandled.ToImmutableHashSet());
                }

            case UsingStatementSyntax usingStmt:
                {
                    if (usingStmt.Expression != null)
                        unhandled.UnionWith(CollectExceptionsFromExpression(context, usingStmt.Expression, settings, semanticModel));

                    if (usingStmt.Declaration != null)
                    {
                        foreach (var v in usingStmt.Declaration.Variables)
                        {
                            if (v.Initializer?.Value != null)
                                unhandled.UnionWith(CollectExceptionsFromExpression(context, v.Initializer.Value, settings, semanticModel));
                        }
                    }

                    var bodyResult = AnalyzeStatementWithExceptions(context, usingStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(bodyResult.EndReachable, unhandled.ToImmutableHashSet());
                }

            case LockStatementSyntax lockStmt:
                {
                    unhandled.UnionWith(CollectExceptionsFromExpression(context, lockStmt.Expression, settings, semanticModel));

                    var bodyResult = AnalyzeStatementWithExceptions(context, lockStmt.Statement, settings);
                    unhandled.UnionWith(bodyResult.UnhandledExceptions);

                    return new FlowWithExceptionsResult(bodyResult.EndReachable, unhandled.ToImmutableHashSet());
                }

            case BreakStatementSyntax:
            case ContinueStatementSyntax:
                {
                    // Terminates current block
                    return new FlowWithExceptionsResult(false, unhandled.ToImmutableHashSet());
                }

            case ReturnStatementSyntax returnStmt:
                {
                    if (returnStmt.Expression is not null)
                    {
                        unhandled.UnionWith(CollectExceptionsFromExpression(context, returnStmt.Expression, settings, semanticModel));
                    }
                    return new FlowWithExceptionsResult(endReachable: false, containsReturn: true, unhandledExceptions: unhandled.ToImmutableHashSet());
                }

            case LocalDeclarationStatementSyntax localDecl
    when localDecl.Declaration.Variables
        .Any(v => v.Initializer?.Value is InvocationExpressionSyntax
               or ElementAccessExpressionSyntax
               or ObjectCreationExpressionSyntax
               or ImplicitObjectCreationExpressionSyntax):
                unhandled.UnionWith(CollectExceptionsFromStatement(context, statement, settings));
                return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());

            case ExpressionStatementSyntax exprStmt
    when exprStmt.Expression.DescendantNodesAndSelf()
        .Any(n => n is InvocationExpressionSyntax
               or ElementAccessExpressionSyntax
               or ObjectCreationExpressionSyntax
               or ImplicitObjectCreationExpressionSyntax):
                unhandled.UnionWith(CollectExceptionsFromStatement(context, statement, settings));
                return new FlowWithExceptionsResult(true, unhandled.ToImmutableHashSet());

            default:
                var flow = semanticModel.AnalyzeControlFlow(statement);

                unhandled.UnionWith(CollectExceptionsFromStatement(context, statement, settings));

                // Fallback: assume it falls through
                return new FlowWithExceptionsResult(
                    flow.Succeeded ? flow.EndPointIsReachable : true,
                    unhandled.ToImmutableHashSet());
        }
    }

    private FlowWithExceptionsResult AnalyzeTryStatement(SyntaxNodeAnalysisContext context, AnalyzerSettings settings, SemanticModel semanticModel, HashSet<INamedTypeSymbol> unhandled, TryStatementSyntax tryStmt)
    {
        // === Analyze try body ===
        var tryResult = AnalyzeBlockWithExceptions(context, tryStmt.Block, settings);
        unhandled.UnionWith(tryResult.UnhandledExceptions);

        bool continuationPossible = tryResult.EndReachable;
        bool containsReturn = tryResult.ContainsReturn;

        var exceptionsLeftToHandle = new HashSet<INamedTypeSymbol>(tryResult.UnhandledExceptions);

        // === Analyze each catch ===
        foreach (var catchClause in tryStmt.Catches)
        {
            var catchResult = AnalyzeCatchClause(
                context,
                catchClause,
                semanticModel,
                [.. tryResult.UnhandledExceptions],
                exceptionsLeftToHandle,
                unhandled,
                settings);

            continuationPossible |= catchResult.EndReachable;
            containsReturn |= catchResult.ContainsReturn;
            unhandled.UnionWith(catchResult.UnhandledExceptions);
        }

        // === Analyze finally ===
        if (tryStmt.Finally?.Block is { } finallyBlock)
        {
            var finallyResult = AnalyzeBlockWithExceptions(context, finallyBlock, settings);

            // Always merge exceptions
            unhandled.UnionWith(finallyResult.UnhandledExceptions);

            if (!finallyResult.EndReachable && !finallyResult.ContainsReturn)
            {
                // Finally always terminates
                unhandled.Clear();
                unhandled.UnionWith(finallyResult.UnhandledExceptions);
                continuationPossible = false;
            }
            else
            {
                continuationPossible = continuationPossible && finallyResult.EndReachable;
                containsReturn |= finallyResult.ContainsReturn;
            }
        }

        return new FlowWithExceptionsResult(
            continuationPossible,
            containsReturn,
            unhandled.ToImmutableHashSet());
    }

    private FlowWithExceptionsResult AnalyzeCatchClause(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        HashSet<INamedTypeSymbol> tryUnhandled,
        HashSet<INamedTypeSymbol> exceptionsLeftToHandle,
        HashSet<INamedTypeSymbol> unhandled,
        AnalyzerSettings settings)
    {
        bool continuationPossible = false;
        bool containsReturn = false;

        // --- catch-all ---
        if (catchClause.Declaration?.Type == null)
        {
            bool handlesAny = exceptionsLeftToHandle.Count > 0;
            if (!handlesAny)
                ReportRedundantCatchAll(context, catchClause);

            var caughtExceptionsInCatch = new HashSet<INamedTypeSymbol>(exceptionsLeftToHandle);

            // Swallow everything the try might throw
            unhandled.RemoveWhere(ex => exceptionsLeftToHandle.Contains(ex));
            exceptionsLeftToHandle.Clear();

            if (catchClause.Block is { } catchBlock)
            {
                var catchResult = AnalyzeBlockWithExceptions(
                    context,
                    catchBlock,
                    settings,
                    caughtExceptionsInCatch);

                unhandled.UnionWith(catchResult.UnhandledExceptions);

                if (handlesAny && catchResult.EndReachable)
                    continuationPossible = true;

                containsReturn |= catchResult.ContainsReturn;

                if (!handlesAny)
                    ReportUnreachableCode(context, catchClause);
            }
        }
        else
        {
            // --- typed catch ---
            var caught = GetCaughtException(catchClause, semanticModel);
            bool handlesAny = false;

            if (caught != null)
            {
                handlesAny = tryUnhandled.Any(ex => IsExceptionCaught(ex, caught));
                if (!handlesAny)
                    ReportRedundantTypedCatchClause(context, catchClause, caught);

                if (handlesAny)
                {
                    unhandled.RemoveWhere(ex => IsExceptionCaught(ex, caught));
                    exceptionsLeftToHandle.RemoveWhere(ex => IsExceptionCaught(ex, caught));
                }
            }

            if (catchClause.Block is { } catchBlock)
            {
                var catchResult = AnalyzeBlockWithExceptions(
                    context,
                    catchBlock,
                    settings,
                    caught != null ? [caught] : null);

                if (handlesAny)
                    unhandled.UnionWith(catchResult.UnhandledExceptions);

                if (handlesAny && catchResult.EndReachable)
                    continuationPossible = true;

                containsReturn |= catchResult.ContainsReturn;

                if (!handlesAny)
                    ReportUnreachableCode(context, catchClause);
            }
        }

        return new FlowWithExceptionsResult(
            continuationPossible,
            containsReturn,
            unhandled.ToImmutableHashSet());
    }

    private static void ReportRedundantTypedCatchClause(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, INamedTypeSymbol caughtType)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleRedundantTypedCatchClause,
            catchClause.Declaration.Type.GetLocation(),
            caughtType.Name));
    }

    private static void ReportRedundantCatchAll(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleRedundantCatchAllClause,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static void ReportUnreachableCode(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        ReportUnreachableCode(context, node.GetLocation());
    }

    private static void ReportUnreachableCode(SyntaxNodeAnalysisContext context, Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleUnreachableCode,
            location));
    }

    private static void ReportUnreachableCodeHidden(SyntaxNodeAnalysisContext context, Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleUnreachableCodeHidden,
            location));
    }

    private Location? GetThrowsAttributeLocation(
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
    public bool EndReachable { get; }
    public bool ContainsReturn { get; } // or more general: HasNormalTermination
    public ImmutableHashSet<INamedTypeSymbol> UnhandledExceptions { get; }

    public FlowWithExceptionsResult(
        bool endReachable,
        bool containsReturn,
        ImmutableHashSet<INamedTypeSymbol> unhandledExceptions)
    {
        EndReachable = endReachable;
        ContainsReturn = containsReturn;
        UnhandledExceptions = unhandledExceptions;
    }

    public FlowWithExceptionsResult(
        bool endReachable,
        ImmutableHashSet<INamedTypeSymbol> unhandledExceptions)
    {
        EndReachable = endReachable;
        ContainsReturn = false;
        UnhandledExceptions = unhandledExceptions;
    }

    public static FlowWithExceptionsResult Unreachable =>
        new(false, false, ImmutableHashSet<INamedTypeSymbol>.Empty);
}