using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static CatchClauseUtils;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddCatchClauseToTryCodeFixProvider)), Shared]
public class AddCatchClauseToTryCodeFixProvider : CodeFixProvider
{
    private const string TitleAddTryCatch = "Add catch clause to surrounding try";

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

        // Get the statement we're analyzing
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
            return;

        // Ensure the statement is within the try block (not catch/finally)
        var tryStatement = node.FirstAncestorOrSelf<TryStatementSyntax>();
        if (tryStatement?.Block is null || !tryStatement.Block.DescendantNodes().Contains(statement))
            return;

        // If the throw is inside a lambda or local function,
        // only offer a fix if it's inside a try block within that same function body.
        if (TryGetEnclosingDeferredContext(statement) is { } deferredContext)
        {
            var enclosingTry = statement.FirstAncestorOrSelf<TryStatementSyntax>();

            if (enclosingTry == null || !deferredContext.Span.Contains(enclosingTry.Span))
            {
                return; // Throw is in a lambda/local function, but not in a try block inside that lambda
            }
        }

        var diagnosticsCount = diagnostics.Length;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnosticsCount > 1 ? TitleAddTryCatch.Replace("clause", "clauses") : TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, statement, diagnostics, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private static SyntaxNode? TryGetEnclosingDeferredContext(SyntaxNode node)
    {
        return (SyntaxNode?)node.FirstAncestorOrSelf<LambdaExpressionSyntax>()
            ?? node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
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

        // Check if the statement is already inside a try-catch block
        var existingTryStatement = statement.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault();

        var existingCatchClause = statement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();

        if (existingTryStatement is not null)
        {
            // Gather existing catch types (simple string comparison)
            var existingCatchTypes = new HashSet<string>(existingTryStatement.Catches
                .Select(c => c.Declaration?.Type?.ToString())
                .Where(t => !string.IsNullOrEmpty(t))!);

            // Determine which exception types are not yet handled
            var newExceptionTypes = exceptionTypeNames
                .Where(t => !existingCatchTypes.Contains(t))
                .Distinct()
                .ToList();

            if (newExceptionTypes.Count == 0)
            {
                return document; // All exceptions already handled
            }

            var catchClausesToAdd = CreateCatchClauses(newExceptionTypes, existingTryStatement.Catches.Count);

            var newTry = existingTryStatement.WithCatches(existingTryStatement.Catches.AddRange(catchClausesToAdd))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(existingTryStatement, newTry);

            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}
