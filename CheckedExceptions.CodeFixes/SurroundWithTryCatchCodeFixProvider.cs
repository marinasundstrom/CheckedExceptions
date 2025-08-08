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
        null!;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnostic = diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is ArgumentSyntax argument)
        {
            node = argument.Expression;
        }

        if (IsInExpressionBody(node, out var rootExpression))
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
                createChangedDocument: c => AddTryCatchAroundStatementAsync(context.Document, statement, diagnostics, WrapStrategy.MinimalTransitive, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private static bool IsInExpressionBody(SyntaxNode node, out ExpressionSyntax? rootExpression)
    {
        if (node is ExpressionSyntax expr)
        {
            // Find the root expression node of a lambda or expression body.
            SyntaxNode n = node;

            while (true)
            {
                var parent = n.Parent;

                if (parent is null)
                    break;

                // 🚨 Short-circuit: we’ve left the realm where an expression body could exist
                if (parent is MemberDeclarationSyntax or StatementSyntax or CompilationUnitSyntax)
                {
                    rootExpression = null;
                    return false;
                }

                if (parent is not (AnonymousFunctionExpressionSyntax or ArrowExpressionClauseSyntax))
                {
                    n = parent;
                }
                else if (n is ExpressionSyntax)
                {
                    expr = (ExpressionSyntax)n;
                    break;
                }
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

    private enum WrapStrategy
    {
        Minimal,    // Wraps the throw site and the immediate dependencies in a try.
        MinimalTransitive, // Wraps the throw site and the transitive dependencies in at try
        Remainder,  // Wraps the remainder of the statements (from the throw site to the end of the block) in a try.
        FullBlock   // Wraps all the statements in the current block in a try.
    }

    private async Task<Document> AddTryCatchAroundStatementAsync(
        Document document,
        StatementSyntax statement,
        IEnumerable<Diagnostic> diagnostics,
        WrapStrategy strategy,
        CancellationToken cancellationToken)
    {
        var exceptionTypeNames = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var type) ? type! : string.Empty);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot;

        // local function: extract start/end indices depending on strategy
        (int start, int end) GetRange(IReadOnlyList<StatementSyntax> statements, int targetIndex)
        {
            switch (strategy)
            {
                case WrapStrategy.Minimal:
                    int start = targetIndex;
                    var dataFlow = semanticModel.AnalyzeDataFlow(statements[targetIndex])!;
                    var readSymbols = new HashSet<ISymbol>(dataFlow.ReadInside, SymbolEqualityComparer.Default);

                    // backtrack dependencies
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
                    var writtenSymbols = new HashSet<ISymbol>(
                        semanticModel.AnalyzeDataFlow(statements[targetIndex])!.WrittenInside,
                        SymbolEqualityComparer.Default);

                    for (int i = targetIndex + 1; i < statements.Count; i++)
                    {
                        var currentFlow = semanticModel.AnalyzeDataFlow(statements[i])!;
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
                    return (start, end);

                case WrapStrategy.MinimalTransitive:
                    return FindTransitiveClosure(targetIndex, statements, semanticModel!);

                case WrapStrategy.Remainder:
                    return (targetIndex, statements.Count - 1);

                case WrapStrategy.FullBlock:
                    return (0, statements.Count - 1);

                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
            }
        }

        if (statement.Parent is BlockSyntax parentBlock)
        {
            var statements = parentBlock.Statements;
            var targetIndex = statements.IndexOf(statement);
            if (targetIndex == -1) return document;

            var (start, end) = GetRange(statements, targetIndex);

            var statementsToWrap = statements
                .Skip(start).Take(end - start + 1)
                .Where(s => s is not LocalFunctionStatementSyntax)
                .ToList();

            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, statementsToWrap);

            newRoot = root.ReplaceNodes(
                statementsToWrap,
                (original, _) => original == statementsToWrap.First() ? tryCatchStatement : null!
            );
        }
        else if (statement.Parent is IfStatementSyntax ifStatement)
        {
            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, [statement]);
            var newIfStatement = ifStatement.WithStatement(Block(tryCatchStatement));
            newRoot = root.ReplaceNode(ifStatement, newIfStatement);
        }
        else if (statement.Parent is WhileStatementSyntax whileStatement)
        {
            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, [statement]);
            var newWhileStatement = whileStatement.WithStatement(Block(tryCatchStatement));
            newRoot = root.ReplaceNode(whileStatement, newWhileStatement);
        }
        else if (statement.Parent is DoStatementSyntax doStatement)
        {
            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, [statement]);
            var newDoStatement = doStatement.WithStatement(Block(tryCatchStatement));
            newRoot = root.ReplaceNode(doStatement, newDoStatement);
        }
        else if (statement.Parent is ForStatementSyntax forStatement)
        {
            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, [statement]);
            var newForStatement = forStatement.WithStatement(Block(tryCatchStatement));
            newRoot = root.ReplaceNode(newForStatement, newForStatement);
        }
        else if (statement.Parent is ForEachStatementSyntax forEachStatement)
        {
            TryStatementSyntax tryCatchStatement = CreateTryStatement(exceptionTypeNames, [statement]);
            var newForEachStatement = forEachStatement.WithStatement(Block(tryCatchStatement));
            newRoot = root.ReplaceNode(forEachStatement, newForEachStatement);
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

            var (start, end) = GetRange(innerStatements, targetIndex);

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

    private static TryStatementSyntax CreateTryStatement(IEnumerable<string> exceptionTypeNames, List<StatementSyntax> statementsToWrap)
    {
        var tryBlock = Block(statementsToWrap).WithAdditionalAnnotations(Formatter.Annotation);
        var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();
        var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

        var tryCatchStatement = TryStatement()
            .WithBlock(tryBlock)
            .WithCatches(List(catchClauses))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return tryCatchStatement;
    }

    private static (int start, int end) FindTransitiveClosure(
        int throwIndex,
        IReadOnlyList<StatementSyntax> statements,
    SemanticModel semanticModel)
    {
        var start = throwIndex;
        var end = throwIndex;

        var included = new HashSet<int> { throwIndex };
        var queue = new Queue<int>();
        queue.Enqueue(throwIndex);

        var readSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var writtenSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        var df = semanticModel.AnalyzeDataFlow(statements[throwIndex])!;
        readSymbols.UnionWith(df.ReadInside);
        writtenSymbols.UnionWith(df.WrittenInside);

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();

            for (int i = 0; i < statements.Count; i++)
            {
                if (included.Contains(i))
                    continue;

                var flow = semanticModel.AnalyzeDataFlow(statements[i])!;
                bool depends =
                    flow.WrittenInside.Any(readSymbols.Contains) ||
                    flow.ReadInside.Any(writtenSymbols.Contains);

                if (depends)
                {
                    included.Add(i);
                    queue.Enqueue(i);

                    start = Math.Min(start, i);
                    end = Math.Max(end, i);

                    readSymbols.UnionWith(flow.ReadInside);
                    writtenSymbols.UnionWith(flow.WrittenInside);
                }
            }
        }

        return (start, end);
    }
}
