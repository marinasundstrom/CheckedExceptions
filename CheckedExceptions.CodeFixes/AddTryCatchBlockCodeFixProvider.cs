using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTryCatchBlockCodeFixProvider)), Shared]
public class AddTryCatchBlockCodeFixProvider : CodeFixProvider
{
    private const string TitleAddTryCatch = "Add try-catch block";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdUnhandled];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;

        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = root.FindNode(diagnostics.First().Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, node, diagnostics, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private async Task<Document> AddTryCatchAsync(Document document, SyntaxNode node, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        // Attempt to retrieve the exception type from diagnostic arguments
        var exceptionTypeNames = diagnostics.Select(diagnostic => diagnostic.Properties["ExceptionType"]);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (node is ThrowStatementSyntax throwStatement && throwStatement.Expression == null)
            return document;

        var statement = node.AncestorsAndSelf(true)
            .OfType<StatementSyntax>()
            .FirstOrDefault();

        var ancestors = statement.AncestorsAndSelf(true).ToList();

        var existingTryStatement = ancestors.OfType<TryStatementSyntax>()
            .FirstOrDefault();

        var existingCatch = ancestors.OfType<CatchClauseSyntax>()
            .FirstOrDefault();

        var existingFinally = ancestors.OfType<FinallyClauseSyntax>()
            .FirstOrDefault();

        var inCatchOrFinallyBlock = existingTryStatement is not null
            && (!existingTryStatement.Catches.Contains(existingCatch)
            || existingTryStatement?.Finally != existingFinally);

        if (!inCatchOrFinallyBlock)
        {
            existingTryStatement = null;
        }

        TryStatementSyntax tryCatchStatement;

        if (existingTryStatement is not null)
        {
            statement = existingTryStatement;

            List<CatchClauseSyntax> newCatchClauses = CreateCatchClauses(exceptionTypeNames);

            newCatchClauses = existingTryStatement.Catches.Concat(newCatchClauses).ToList();

            tryCatchStatement = existingTryStatement.WithCatches(SyntaxFactory.List(newCatchClauses));
        }
        else
        {
            var tryBlock = SyntaxFactory.Block(statement);

            var count = ancestors.OfType<TryStatementSyntax>().Count();

            List<CatchClauseSyntax> catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            tryCatchStatement = SyntaxFactory.TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(SyntaxFactory.List(catchClauses));
        }

        var newRoot = root.ReplaceNode(statement, tryCatchStatement.NormalizeWhitespace(elasticTrivia: true));

        return document.WithSyntaxRoot(newRoot);
    }

    private static List<CatchClauseSyntax> CreateCatchClauses(IEnumerable<string?> exceptionTypeNames, int level = 0)
    {
        List<CatchClauseSyntax> catchClauses = new List<CatchClauseSyntax>();

        string catchExceptionVariableName;

        if (level == 0)
        {
            catchExceptionVariableName = "ex";
        }
        else
        {
            catchExceptionVariableName = $"ex{level + 1}";
        }

        foreach (var exceptionTypeName in exceptionTypeNames.Distinct())
        {
            var exceptionType = SyntaxFactory.ParseTypeName(exceptionTypeName);

            var catchClause = SyntaxFactory.CatchClause()
                .WithDeclaration(
                    SyntaxFactory.CatchDeclaration(exceptionType)
                    .WithIdentifier(SyntaxFactory.Identifier(catchExceptionVariableName)))
                .WithBlock(SyntaxFactory.Block()).WithAdditionalAnnotations(Formatter.Annotation);

            catchClauses.Add(catchClause);
        }

        return catchClauses;
    }
}
