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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTryCatchBlockCodeFixProvider)), Shared]
public class AddTryCatchBlockCodeFixProvider : CodeFixProvider
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

        StatementSyntax? statement = null;

        if (node is GlobalStatementSyntax globalStatement)
        {
            statement = globalStatement.Statement;
        }

        if (statement is null)
        {
            // Register the code fix only if the node is a statement or within a statement
            statement = node.FirstAncestorOrSelf<StatementSyntax>();
        }

        if (statement is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, statement, diagnostics, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private async Task<Document> AddTryCatchAsync(Document document, StatementSyntax statement, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        // Retrieve exception types from diagnostics
        var exceptionTypeNames = diagnostics
            .Select(diagnostic => diagnostic.Properties.ContainsKey("ExceptionType") ? diagnostic.Properties["ExceptionType"]! : string.Empty);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        SyntaxNode newRoot;

        if (statement.Parent is BlockSyntax parentBlock)
        {
            // Existing block-scope logic
            var statements = parentBlock.Statements;
            var targetIndex = statements.IndexOf(statement);
            if (targetIndex == -1)
                return document;

            // Perform data flow analysis on the target statement
            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;

            var variablesToTrack = new HashSet<ISymbol>(dataFlow.ReadInside.Concat(dataFlow.WrittenInside), SymbolEqualityComparer.Default);

            // Determine the range of statements to include using flow analysis
            int start = targetIndex;
            int end = targetIndex;

            // Expand upwards to include related statements
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                var currentStatement = statements[i];
                var currentDataFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentDataFlow.WrittenOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentDataFlow.ReadInside.Any(v => variablesToTrack.Contains(v)))
                {
                    start = i;
                    variablesToTrack.UnionWith(currentDataFlow.ReadInside);
                    variablesToTrack.UnionWith(currentDataFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            // Expand downwards to include related statements
            for (int i = targetIndex + 1; i < statements.Count; i++)
            {
                var currentStatement = statements[i];
                var currentDataFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentDataFlow.ReadOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentDataFlow.WrittenInside.Any(v => variablesToTrack.Contains(v)))
                {
                    end = i;
                    variablesToTrack.UnionWith(currentDataFlow.ReadInside);
                    variablesToTrack.UnionWith(currentDataFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = statements.Skip(start).Take(end - start + 1).ToList();

            var tryBlock = Block(statementsToWrap)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryCatchStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            newRoot = root.ReplaceNodes(
                statementsToWrap,
                (original, rewritten) =>
                    original == statementsToWrap.First()
                        ? tryCatchStatement
                        : null!
            );
        }
        else if (statement.Parent is GlobalStatementSyntax globalStatement &&
                 root is CompilationUnitSyntax compilationUnit)
        {
            var globalStatements = compilationUnit.Members
                .OfType<GlobalStatementSyntax>()
                .ToList();

            var innerStatements = globalStatements
                .Select(gs => gs.Statement)
                .ToList();

            var targetIndex = innerStatements.IndexOf(statement);
            if (targetIndex == -1)
                return document;

            var variablesToTrack = new HashSet<ISymbol>(
                semanticModel.AnalyzeDataFlow(statement)!.ReadInside
                .Concat(semanticModel.AnalyzeDataFlow(statement)!.WrittenInside),
                SymbolEqualityComparer.Default);

            int start = targetIndex;
            int end = targetIndex;

            for (int i = targetIndex - 1; i >= 0; i--)
            {
                var currentStatement = innerStatements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentFlow.WrittenOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentFlow.ReadInside.Any(v => variablesToTrack.Contains(v)))
                {
                    start = i;
                    variablesToTrack.UnionWith(currentFlow.ReadInside);
                    variablesToTrack.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            for (int i = targetIndex + 1; i < innerStatements.Count; i++)
            {
                var currentStatement = innerStatements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentFlow.ReadOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentFlow.WrittenInside.Any(v => variablesToTrack.Contains(v)))
                {
                    end = i;
                    variablesToTrack.UnionWith(currentFlow.ReadInside);
                    variablesToTrack.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = innerStatements.Skip(start).Take(end - start + 1).ToList();

            var tryBlock = Block(statementsToWrap)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var count = globalStatement.Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var tryGlobalStatement = GlobalStatement(tryStatement)
                .WithTriviaFrom(globalStatements[start])
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Replace all statements from start to end with one try/catch statement
            newRoot = root.ReplaceNodes(
                globalStatements.Skip(start).Take(end - start + 1),
                (original, rewritten) =>
                    original == globalStatements[start] ? tryGlobalStatement : null!
            );
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(newRoot);
    }
}