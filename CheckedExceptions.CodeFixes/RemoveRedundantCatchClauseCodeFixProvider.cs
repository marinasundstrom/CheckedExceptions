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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveRedundantCatchClauseCodeFixProvider)), Shared]
public class RemoveRedundantCatchClauseCodeFixProvider : CodeFixProvider
{
    private const string TitleRemoveRedundantCatchClause = "Remove redundant catch clause";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnostic = diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var catchClause = node.AncestorsAndSelf().OfType<CatchClauseSyntax>().First();

        var tryStatement = catchClause.Parent as TryStatementSyntax;

        string title = TitleRemoveRedundantCatchClause;

        if (tryStatement is not null)
        {
            if (tryStatement.Catches.Count == 1)
            {
                title = title.Replace(title, "Remove redundant try/catch");
            }
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => RemoveRedundantCatchClauseAsync(context.Document, catchClause, diagnostics, c),
                equivalenceKey: TitleRemoveRedundantCatchClause),
            diagnostics);
    }

    private async Task<Document> RemoveRedundantCatchClauseAsync(Document document, CatchClauseSyntax catchClause, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var exceptionTypeNames = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var type) ? type! : string.Empty);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var tryStatement = catchClause.Parent as TryStatementSyntax;

        if (tryStatement is not null)
        {
            if (tryStatement.Catches.Count == 1)
            {
                // Get the current node in the tree
                var nodeInRoot = root.FindNode(tryStatement.Span);

                // What we want to insert instead
                var liftedStatements = tryStatement.Block.Statements;

                SyntaxNode newRoot;

                if (nodeInRoot is GlobalStatementSyntax global)
                {
                    // Wrap each lifted statement in its own GlobalStatementSyntax
                    var newGlobals = liftedStatements.Select(
                        s => GlobalStatement(s)
                            .WithLeadingTrivia(global.GetLeadingTrivia())
                            .WithTrailingTrivia(global.GetTrailingTrivia()));

                    newRoot = root.ReplaceNode(global, newGlobals);
                }
                else if (nodeInRoot is TryStatementSyntax tryNode)
                {
                    // Normal case inside a block

                    var annotatedStatements = liftedStatements
                        .Select(s => s.WithAdditionalAnnotations(Formatter.Annotation));

                    newRoot = root.ReplaceNode(tryNode, annotatedStatements);
                }
                else
                {
                    // Fallback (shouldn’t really happen)
                    return document;
                }

                return document.WithSyntaxRoot(newRoot);
            }
            else
            {
                var newRoot = root.RemoveNode(catchClause, SyntaxRemoveOptions.AddElasticMarker);
                return document.WithSyntaxRoot(newRoot);
            }
        }

        return document;
    }
}
