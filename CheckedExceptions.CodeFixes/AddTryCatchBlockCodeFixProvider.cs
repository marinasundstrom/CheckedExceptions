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

        // Register the code fix only if the node is a statement or within a statement
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
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

        var parentBlock = statement.Parent as BlockSyntax;
        if (parentBlock is null)
            return document; // Cannot proceed if the parent is not a block

        var statements = parentBlock.Statements;
        var targetIndex = statements.IndexOf(statement);

        if (targetIndex is -1)
            return document; // Statement not found in the parent block

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

        // Extract the related statements
        var statementsToWrap = statements.Skip(start).Take(end - start + 1).ToList();

        // Create the try block
        var tryBlock = Block(statementsToWrap)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();

        // Create catch clauses based on exception types
        List<CatchClauseSyntax> catchClauses = CreateCatchClauses(exceptionTypeNames, count);

        // Construct the try-catch statement
        TryStatementSyntax tryCatchStatement = TryStatement()
            .WithBlock(tryBlock)
            .WithCatches(List(catchClauses))
            .WithAdditionalAnnotations(Formatter.Annotation);

        // Replace the original statements with the try-catch statement
        var newRootReplace = root.ReplaceNodes(
            statementsToWrap,
            (original, rewritten) => original == statementsToWrap.First() ? tryCatchStatement.WithAdditionalAnnotations(Formatter.Annotation) : null!
        );

        return document.WithSyntaxRoot(newRootReplace);
    }
}