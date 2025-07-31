using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// From method symbol
    /// </summary>
    private void CheckForRedundantThrowsDeclarations(
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

        // Collect all actually escaping exceptions
        var actual = CollectThrownExceptions(methodSymbol, context.Compilation, context.Options);

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

    /// <summary>
    /// For local functions and lambda syntaxes
    /// </summary>
    private void CheckForRedundantThrowsDeclarations(
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
        var actual = CollectThrownExceptions(methodSymbol, semanticModel.Compilation, context.Options);

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
        var actual = CollectThrownExceptions(node, semanticModel.Compilation, context.Options);

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
        AnalyzerOptions analyzerOptions)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var syntax = syntaxRef.GetSyntax();

        return CollectThrownExceptions(syntax, compilation, analyzerOptions);
    }

    private ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
         SyntaxNode node,
         Compilation compilation,
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
            reportDiagnostic: _ => { },
            isSupportedDiagnostic: _ => true,
            cancellationToken: default);

        var settings = GetAnalyzerSettings(context.Options);

        if (body is not null)
        {
            var unhandled = CollectAllEscapingExceptions(context, body, settings);
            return [.. unhandled.OfType<INamedTypeSymbol>()];
        }
        else if (expressionBody is not null)
        {
            // Collect exceptions directly from the expression
            var exceptions = CollectExceptionsFromExpression(context, expressionBody, settings, semanticModel);
            return [.. exceptions.OfType<INamedTypeSymbol>()];
        }

        return [];
    }

    private HashSet<INamedTypeSymbol> CollectAllEscapingExceptions(
        SyntaxNodeAnalysisContext context,
        BlockSyntax block,
        AnalyzerSettings settings)
    {
        var unhandledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (statement is TryStatementSyntax tryStatement)
            {
                var innerUnhandled = CollectAllEscapingExceptions(context, tryStatement.Block, settings);

                var caughtExceptions = GetCaughtExceptions(tryStatement.Catches, context.SemanticModel);
                innerUnhandled.RemoveWhere(exceptionType =>
                    IsExceptionCaught(exceptionType, caughtExceptions));

                unhandledExceptions.UnionWith(innerUnhandled);

                // 🔑 NEW: also recurse into catch blocks themselves
                foreach (var catchClause in tryStatement.Catches)
                {
                    if (catchClause.Block is not null)
                    {
                        var catchUnhandled = CollectAllEscapingExceptions(context, catchClause.Block, settings);
                        unhandledExceptions.UnionWith(catchUnhandled);
                    }
                }

                // Finally, consider `finally` block if present
                if (tryStatement.Finally?.Block is { } finallyBlock)
                {
                    var finallyUnhandled = CollectAllEscapingExceptions(context, finallyBlock, settings);
                    unhandledExceptions.UnionWith(finallyUnhandled);
                }
            }
            else
            {
                var statementExceptions = CollectExceptionsFromStatement(context, statement, settings);
                unhandledExceptions.UnionWith(statementExceptions);
            }
        }

        return unhandledExceptions;
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