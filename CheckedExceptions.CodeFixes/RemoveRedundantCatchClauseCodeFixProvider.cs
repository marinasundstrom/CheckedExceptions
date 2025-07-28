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

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleRemoveRedundantCatchClause,
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

        var newRoot = root.RemoveNode(catchClause, SyntaxRemoveOptions.AddElasticMarker);

        return document.WithSyntaxRoot(newRoot);
    }
}
