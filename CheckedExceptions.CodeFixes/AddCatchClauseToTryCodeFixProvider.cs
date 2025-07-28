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

        // This is the throw site — could be any expression
        var throwSite = node;

        // 🔎 Case 1: expression-bodied lambda → no fix possible
        if (TryGetEnclosingDeferredContext(throwSite) is LambdaExpressionSyntax lambda &&
            lambda.Body is ExpressionSyntax)
        {
            return; // ❌ can't wrap an expression-bodied lambda in try/catch
        }

        // 🔎 Case 2: inside any lambda or local function
        if (TryGetEnclosingDeferredContext(throwSite) is { } deferredContext)
        {
            var innerTry = throwSite.FirstAncestorOrSelf<TryStatementSyntax>();
            if (innerTry == null || !deferredContext.Span.Contains(innerTry.Span))
            {
                return; // ❌ inside deferred context, but not protected by a nested try
            }
        }

        // ✅ Normal case: check for enclosing try that covers this throw site
        var tryStatement = throwSite.FirstAncestorOrSelf<TryStatementSyntax>();
        if (tryStatement?.Block is null || !tryStatement.Block.DescendantNodes().Contains(throwSite))
        {
            return; // ❌ not inside runtime flow of a try
        }

        var diagnosticsCount = diagnostics.Length;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnosticsCount > 1 ? TitleAddTryCatch.Replace("clause", "clauses") : TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, (StatementSyntax)throwSite, diagnostics, c),
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
