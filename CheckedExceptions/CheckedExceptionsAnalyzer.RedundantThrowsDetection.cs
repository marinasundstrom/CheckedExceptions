using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
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
            if (!actual.Contains(declaredType, SymbolEqualityComparer.Default))
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

    private ImmutableHashSet<INamedTypeSymbol> CollectThrownExceptions(
        IMethodSymbol method,
        Compilation compilation,
        AnalyzerOptions analyzerOptions)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var node = syntaxRef.GetSyntax();
        if (node is not BaseMethodDeclarationSyntax methodDecl || methodDecl.Body == null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var semanticModel = compilation.GetSemanticModel(node.SyntaxTree);

        var context = new SyntaxNodeAnalysisContext(
            methodDecl,
            semanticModel,
            options: analyzerOptions,
            reportDiagnostic: _ => { },
            isSupportedDiagnostic: _ => true,
            cancellationToken: default);

        // Reuse existing unhandled exception collector
        var unhandled = CollectAllEscapingExceptions(context, methodDecl.Body, GetAnalyzerSettings(context.Options));

        return [.. unhandled.ToImmutableHashSet(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()];
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
      IMethodSymbol method,
      INamedTypeSymbol declaredType,
      Compilation compilation)
    {
        foreach (var attr in method.GetAttributes())
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