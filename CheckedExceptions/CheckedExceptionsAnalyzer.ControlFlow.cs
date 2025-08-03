using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

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

    private void CheckForRedundantThrowsDeclarations_ExpressionBodiedProperty(
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
        BlockSyntax? body = null;
        ExpressionSyntax? expressionBody = null;

        switch (node)
        {
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

        if (body is not null)
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
      BlockSyntax block,
      AnalyzerSettings settings)
    {
        var semanticModel = context.SemanticModel;
        var flow = semanticModel.AnalyzeControlFlow(block);

        if (!flow.Succeeded || !flow.StartPointIsReachable)
            return FlowWithExceptionsResult.Unreachable;

        var unhandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        bool reachable = true;

        foreach (var statement in block.Statements)
        {
            if (!reachable)
            {
                // 🚩 We already know the block can’t continue past here
                var firstUnreachable = statement; // the first statement we know is unreachable
                var lastStatement = block.Statements.Last();

                // Span from start of firstUnreachable to end of lastStatement
                var span = TextSpan.FromBounds(
                    firstUnreachable.FullSpan.Start,
                    lastStatement.FullSpan.End);

                var location = Location.Create(block.SyntaxTree, span);

                ReportUnreachableCode(context, location);
                break;
            }

            if (statement is TryStatementSyntax tryStmt)
            {
                var tryResult = AnalyzeBlockWithExceptions(context, tryStmt.Block, settings);
                unhandled.UnionWith(tryResult.UnhandledExceptions);

                bool continuationPossible = false;

                // Path 1: try block falls through
                if (tryResult.EndReachable)
                    continuationPossible = true;

                var exceptionsLeftToHandle = new HashSet<INamedTypeSymbol>(tryResult.UnhandledExceptions);

                // Path 2: any catch that actually handles something and falls through
                foreach (var catchClause in tryStmt.Catches)
                {
                    if (catchClause.Declaration?.Type == null)
                    {
                        bool isCatchRedundant = exceptionsLeftToHandle.Count == 0;
                        if (isCatchRedundant)
                        {
                            // 🚩 Redundant catch-all
                            context.ReportDiagnostic(Diagnostic.Create(
                                RuleRedundantCatchAllClause,
                                catchClause.CatchKeyword.GetLocation()));
                        }

                        // Always analyze body for unreachable diagnostics
                        if (catchClause.Block != null)
                        {
                            var catchResult = AnalyzeBlockWithExceptions(context, catchClause.Block, settings);
                            unhandled.UnionWith(catchResult.UnhandledExceptions);

                            // 🔑 Even if body falls through, do NOT flip continuationPossible
                            // because a redundant catch-all doesn't make later code reachable.
                            if (!isCatchRedundant && catchResult.EndReachable)
                                continuationPossible = true;

                            if (isCatchRedundant)
                            {
                                ReportUnreachableCode(context, catchClause);
                            }
                        }

                        // Swallow all remaining exceptions
                        unhandled.UnionWith(exceptionsLeftToHandle);
                        exceptionsLeftToHandle.Clear();
                        break;
                    }

                    // typed catch
                    var caught = GetCaughtException(catchClause, semanticModel);
                    if (caught != null)
                    {
                        bool handlesAny = tryResult.UnhandledExceptions.Any(ex => IsExceptionCaught(ex, caught));
                        if (!handlesAny)
                        {
                            // 🚩 Redundant typed catch
                            context.ReportDiagnostic(Diagnostic.Create(
                                RuleRedundantTypedCatchClause,
                                catchClause.Declaration.Type.GetLocation(),
                                caught.Name));

                            if (catchClause.Block != null)
                            {
                                ReportUnreachableCode(context, catchClause);
                            }

                            continue; // skip analyzing body since it's unreachable
                        }

                        // Otherwise, remove handled exceptions
                        unhandled.RemoveWhere(ex => IsExceptionCaught(ex, caught));
                        exceptionsLeftToHandle.RemoveWhere(ex => IsExceptionCaught(ex, caught));
                    }

                    if (catchClause.Block != null)
                    {
                        var catchResult = AnalyzeBlockWithExceptions(context, catchClause.Block, settings);
                        unhandled.UnionWith(catchResult.UnhandledExceptions);

                        if (catchResult.EndReachable)
                            continuationPossible = true;
                    }
                }

                // Path 3: finally falls through
                if (tryStmt.Finally?.Block is { } finallyBlock)
                {
                    var finallyResult = AnalyzeBlockWithExceptions(context, finallyBlock, settings);
                    unhandled.UnionWith(finallyResult.UnhandledExceptions);

                    if (finallyResult.EndReachable)
                        continuationPossible = true;
                }

                reachable = continuationPossible;
            }
            else if (statement is ThrowStatementSyntax or ReturnStatementSyntax)
            {
                var stmtExceptions = CollectExceptionsFromStatement(context, statement, settings);
                unhandled.UnionWith(stmtExceptions);

                // 🚩 Throw/return terminates
                reachable = false;
            }
            else
            {
                // Normal statement
                var stmtExceptions = CollectExceptionsFromStatement(context, statement, settings);
                unhandled.UnionWith(stmtExceptions);

                // still reachable unless it contains throw/return (already handled)
            }
        }

        return new FlowWithExceptionsResult(
            reachable,
            unhandled.ToImmutableHashSet(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>().ToImmutableHashSet());
    }

    private static void ReportUnreachableThrow(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            RuleUnreachableThrow,
            node.GetLocation()));
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

    private INamedTypeSymbol? GetExceptionTypeFromNode(SyntaxNode throwNode, SemanticModel semanticModel)
    {
        switch (throwNode)
        {
            case ThrowStatementSyntax throwStmt
                when throwStmt.Expression is ObjectCreationExpressionSyntax obj:
                {
                    var type = semanticModel.GetTypeInfo(obj.Type).Type as INamedTypeSymbol;
                    return type;
                }

            case ThrowExpressionSyntax throwExpr
                when throwExpr.Expression is ObjectCreationExpressionSyntax obj:
                {
                    var type = semanticModel.GetTypeInfo(obj.Type).Type as INamedTypeSymbol;
                    return type;
                }

                // TODO: handle invocation and rethrow cases if needed
        }

        return null;
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
    public ImmutableHashSet<INamedTypeSymbol> UnhandledExceptions { get; }

    public FlowWithExceptionsResult(
        bool endReachable,
        ImmutableHashSet<INamedTypeSymbol> unhandled)
    {
        EndReachable = endReachable;
        UnhandledExceptions = unhandled;
    }

    public static FlowWithExceptionsResult Unreachable =>
        new(false, ImmutableHashSet<INamedTypeSymbol>.Empty);
}