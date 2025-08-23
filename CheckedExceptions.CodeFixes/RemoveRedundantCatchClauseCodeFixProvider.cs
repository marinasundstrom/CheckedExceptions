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
        [CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause, CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause, CheckedExceptionsAnalyzer.DiagnosticIdCatchHandlesNoRemainingExceptions, CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause];

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

        if (diagnostics.Length is 1 && tryStatement?.Catches.Count is 1)
        {
            title = "Remove redundant try/catch";
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => RemoveRedundantCatchClausesAsync(context.Document, diagnostics, c),
                equivalenceKey: TitleRemoveRedundantCatchClause),
            diagnostics);
    }

    private async Task<Document> RemoveRedundantCatchClausesAsync(Document document, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var orderedDiagnostics = diagnostics
            .OrderByDescending(d => d.Location.SourceSpan.Start);

        var newRoot = root;

        foreach (var diagnostic in orderedDiagnostics)
        {
            var clause = newRoot.FindNode(diagnostic.Location.SourceSpan).AncestorsAndSelf().OfType<CatchClauseSyntax>().First();
            var tryStatement = clause.Parent as TryStatementSyntax;

            if (tryStatement is not null && tryStatement.Catches.Count == 1)
            {
                var nodeInRoot = newRoot.FindNode(tryStatement.Span);
                var liftedStatements = tryStatement.Block.Statements;

                if (nodeInRoot is GlobalStatementSyntax global)
                {
                    var newGlobals = liftedStatements.Select(s => GlobalStatement(s)
                        .WithLeadingTrivia(global.GetLeadingTrivia())
                        .WithTrailingTrivia(global.GetTrailingTrivia()));

                    newRoot = newRoot.ReplaceNode(global, newGlobals);
                }
                else if (nodeInRoot is TryStatementSyntax tryNode)
                {
                    var annotatedStatements = liftedStatements
                        .Select(s => s.WithAdditionalAnnotations(Formatter.Annotation));

                    newRoot = newRoot.ReplaceNode(tryNode, annotatedStatements);
                }
            }
            else
            {
                var currentClause = newRoot.FindNode(clause.Span).AncestorsAndSelf().OfType<CatchClauseSyntax>().First();
                newRoot = newRoot.RemoveNode(currentClause, SyntaxRemoveOptions.AddElasticMarker);
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
