using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static CatchClauseUtils;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SurroundWithTryCatchCodeFixProvider)), Shared]
public class SurroundWithTryCatchCodeFixProvider : CodeFixProvider
{
    private const string TitleAddTryCatch = "Surround with try/catch";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdUnhandled];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnostic = diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (IsExpressionBody(node, out var rootExpression))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: TitleAddTryCatch,
                    createChangedDocument: c => AddTryCatchToExpressionBodyAsync(context.Document, rootExpression!, diagnostics, c),
                    equivalenceKey: TitleAddTryCatch),
                diagnostics);

            return;
        }

        StatementSyntax? statement = node is GlobalStatementSyntax g ? g.Statement : node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAroundStatementAsync(context.Document, statement, diagnostics, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private static bool IsExpressionBody(SyntaxNode node, out ExpressionSyntax? rootExpression)
    {
        if (node is ExpressionSyntax expr)
        {
            // Find the root expression node.
            // Some members have ArrowExpressionClauseSyntax
            // but for lambda expressions you need to stop by it.
            while (expr.Parent is ExpressionSyntax expr2
                and not AnonymousFunctionExpressionSyntax)
            {
                expr = expr2;
            }

            switch (expr.Parent)
            {
                case AnonymousFunctionExpressionSyntax le when le.ExpressionBody == expr:
                    rootExpression = expr;
                    return true;

                case ArrowExpressionClauseSyntax ace when ace.Parent
                    is BaseMethodDeclarationSyntax
                    || ace.Parent is BasePropertyDeclarationSyntax
                    || ace.Parent is AccessorDeclarationSyntax
                    || ace.Parent is LocalFunctionStatementSyntax:
                    rootExpression = expr;
                    return true;
            }
        }
        rootExpression = null;
        return false;
    }

    private async Task<Document> AddTryCatchToExpressionBodyAsync(Document document, ExpressionSyntax expression, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        SyntaxNode newRoot = default!;

        var tryCatchStatement = CreateTryCatch(expression, diagnostics);
        var block = Block(SingletonList(tryCatchStatement));

        if (expression.Parent is AnonymousFunctionExpressionSyntax lambdaExpression)
        {
            var newLambdaExpression = lambdaExpression
                .WithExpressionBody(null)
                .WithBody(block.WithAdditionalAnnotations(Formatter.Annotation))
                .WithAdditionalAnnotations(Formatter.Annotation);

            newRoot = root.ReplaceNode(lambdaExpression, newLambdaExpression);
        }
        else if (expression.Parent is ArrowExpressionClauseSyntax arrowExpressionClause)
        {
            if (arrowExpressionClause.Parent is LocalFunctionStatementSyntax localFunctionStatement)
            {
                var newLocalFunctionStatement = localFunctionStatement
                    .WithExpressionBody(null)
                    .WithSemicolonToken(Token(SyntaxKind.None))
                    .WithBody(block.WithAdditionalAnnotations(Formatter.Annotation))
                    .WithTrailingTrivia(localFunctionStatement.SemicolonToken.TrailingTrivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                newRoot = root.ReplaceNode(localFunctionStatement, newLocalFunctionStatement);
            }
            else if (arrowExpressionClause.Parent is BaseMethodDeclarationSyntax methodDeclarationSyntax)
            {
                var newMethodDeclarationSyntax = methodDeclarationSyntax
                    .WithExpressionBody(null)
                    .WithSemicolonToken(Token(SyntaxKind.None))
                    .WithBody(block)
                    .WithTrailingTrivia(methodDeclarationSyntax.SemicolonToken.TrailingTrivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                newRoot = root.ReplaceNode(methodDeclarationSyntax, newMethodDeclarationSyntax);
            }
            else if (arrowExpressionClause.Parent is PropertyDeclarationSyntax propertyDeclarationSyntax)
            {
                var getAccessor = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(block);

                var accessorList = AccessorList(SingletonList(getAccessor));

                var newPropertyDeclarationSyntax = propertyDeclarationSyntax
                    .WithExpressionBody(null)
                    .WithSemicolonToken(Token(SyntaxKind.None))
                    .WithAccessorList(accessorList)
                    .WithTrailingTrivia(propertyDeclarationSyntax.SemicolonToken.TrailingTrivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                newRoot = root.ReplaceNode(propertyDeclarationSyntax, newPropertyDeclarationSyntax);
            }
            else if (arrowExpressionClause.Parent is AccessorDeclarationSyntax accessorDeclarationSyntax)
            {
                var accessorList = accessorDeclarationSyntax.Parent as AccessorListSyntax;

                if (accessorList is null)
                    return document;

                var propertyDeclaration = accessorList.Parent as BasePropertyDeclarationSyntax;

                if (propertyDeclaration is null)
                    return document;

                AccessorDeclarationSyntax newAccessorDeclarationSyntax = accessorDeclarationSyntax
                    .WithExpressionBody(null)
                    .WithSemicolonToken(Token(SyntaxKind.None))
                    .WithBody(block)
                    .WithTrailingTrivia(accessorDeclarationSyntax.SemicolonToken.TrailingTrivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                var newAccessorList = accessorList.ReplaceNode(accessorDeclarationSyntax, newAccessorDeclarationSyntax);
                var newPropertyDeclaration = propertyDeclaration.ReplaceNode(accessorList, newAccessorList);

                var str = newPropertyDeclaration.ToFullString();

                newRoot = root.ReplaceNode(propertyDeclaration, newPropertyDeclaration);
            }
            else
            {
                return document;
            }
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static TryStatementSyntax CreateTryCatch(ExpressionSyntax expression, ImmutableArray<Diagnostic> diagnostics)
    {
        var exceptionTypeNames = diagnostics
       .Select(d => d.Properties.TryGetValue("ExceptionType", out var type) ? type! : string.Empty);

        var expressionStatement = ReturnStatement(expression);

        var tryBlock = Block(SingletonList(expressionStatement)).WithAdditionalAnnotations(Formatter.Annotation);

        int count = exceptionTypeNames.Count();

        var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

        return TryStatement(tryBlock.WithTrailingTrivia(EndOfLine("\n")), List(catchClauses), null)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private async Task<Document> AddTryCatchAroundStatementAsync(Document document, StatementSyntax statement, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var exceptionTypeNames = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var type) ? type! : string.Empty);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot;

        if (statement.Parent is BlockSyntax parentBlock)
        {
            var statements = parentBlock.Statements;
            var targetIndex = statements.IndexOf(statement);
            if (targetIndex == -1) return document;

            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;
            var readSymbols = new HashSet<ISymbol>(dataFlow.ReadInside, SymbolEqualityComparer.Default);

            int start = targetIndex;
            for (int i = 0; i < targetIndex; i++)
            {
                var flow = semanticModel.AnalyzeDataFlow(statements[i])!;
                if (flow.WrittenInside.Any(readSymbols.Contains))
                {
                    start = i;
                    break;
                }
            }

            int end = targetIndex;
            var writtenSymbols = new HashSet<ISymbol>(semanticModel.AnalyzeDataFlow(statements[targetIndex])!.WrittenInside, SymbolEqualityComparer.Default);

            for (int i = targetIndex + 1; i < statements.Count; i++)
            {
                var current = statements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(current)!;

                if (currentFlow.ReadInside.Any(writtenSymbols.Contains))
                {
                    end = i;
                    writtenSymbols.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = statements
                .Skip(start).Take(end - start + 1)
                .Where(s => s is not LocalFunctionStatementSyntax)
                .ToList();

            var tryBlock = Block(statementsToWrap).WithAdditionalAnnotations(Formatter.Annotation);
            var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryCatchStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            newRoot = root.ReplaceNodes(
                statementsToWrap,
                (original, _) => original == statementsToWrap.First() ? tryCatchStatement : null!
            );
        }
        else if (statement.Parent is GlobalStatementSyntax globalStatement &&
                 root is CompilationUnitSyntax compilationUnit)
        {
            var members = compilationUnit.Members;
            var globalStatementsWithIndex = members
                .Select((m, i) => (m, i))
                .Where(t => t.m is GlobalStatementSyntax)
                .Select(t => ((GlobalStatementSyntax)t.m, t.i))
                .ToList();

            var innerStatements = globalStatementsWithIndex.Select(t => t.Item1.Statement).ToList();
            var targetIndex = innerStatements.IndexOf(statement);
            if (targetIndex == -1) return document;

            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;
            var readSymbols = new HashSet<ISymbol>(dataFlow.ReadInside, SymbolEqualityComparer.Default);

            int start = targetIndex;
            for (int i = 0; i < targetIndex; i++)
            {
                var flow = semanticModel.AnalyzeDataFlow(innerStatements[i])!;
                if (flow.WrittenInside.Any(readSymbols.Contains))
                {
                    start = i;
                    break;
                }
            }

            int end = targetIndex;
            var writtenSymbols = new HashSet<ISymbol>(semanticModel.AnalyzeDataFlow(innerStatements[targetIndex])!.WrittenInside, SymbolEqualityComparer.Default);

            for (int i = targetIndex + 1; i < innerStatements.Count; i++)
            {
                var current = innerStatements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(current)!;

                if (currentFlow.ReadInside.Any(writtenSymbols.Contains))
                {
                    end = i;
                    writtenSymbols.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = innerStatements
                .Skip(start).Take(end - start + 1)
                .Where(s => s is not LocalFunctionStatementSyntax)
                .ToList();

            var tryBlock = Block(statementsToWrap).WithAdditionalAnnotations(Formatter.Annotation);
            var count = globalStatement.Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var tryGlobalStatement = GlobalStatement(tryStatement)
                .WithTriviaFrom(globalStatementsWithIndex[start].Item1)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var indicesToWrap = globalStatementsWithIndex
                .Skip(start).Take(end - start + 1)
                .Select(t => t.i).ToHashSet();

            var newMembers = new List<MemberDeclarationSyntax>();
            for (int i = 0; i < members.Count; i++)
            {
                if (i == globalStatementsWithIndex[start].i)
                {
                    newMembers.Add(tryGlobalStatement);
                }
                else if (!indicesToWrap.Contains(i))
                {
                    newMembers.Add(members[i]);
                }
            }

            newRoot = compilationUnit.WithMembers(List(newMembers));
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
