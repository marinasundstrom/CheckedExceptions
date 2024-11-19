using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;

namespace CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTryCatchBlockCodeFixProvider)), Shared]
public class AddTryCatchBlockCodeFixProvider : CodeFixProvider
{
    private const string TitleAddTryCatch = "Add try-catch block";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();

        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, node, diagnostic, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostic);
    }

    private async Task<Document> AddTryCatchAsync(Document document, SyntaxNode node, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        string? exceptionTypeName = null;

        // Attempt to retrieve the exception type from diagnostic arguments
        if (diagnostic.Properties.TryGetValue("ExceptionType", out var exceptionTypeFromDiagnostic))
        {
            exceptionTypeName = exceptionTypeFromDiagnostic;
        }

        if (string.IsNullOrWhiteSpace(exceptionTypeName))
            return document;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (node is ThrowStatementSyntax throwStatement && throwStatement.Expression == null)
            return document;

        var exceptionType = SyntaxFactory.ParseTypeName(exceptionTypeName);

        if (exceptionType == null)
            return document;

        var statement = node as StatementSyntax;

        if (statement is null)
        {
            SyntaxNode? nodeToCheck = node;
            while (nodeToCheck is not StatementSyntax)
            {
                nodeToCheck = nodeToCheck.Parent;
            }

            statement = nodeToCheck as StatementSyntax;
        }

        // Create a try-catch block
        var tryBlock = SyntaxFactory.Block(statement);

        var catchClause = SyntaxFactory.CatchClause()
            .WithDeclaration(
                SyntaxFactory.CatchDeclaration(exceptionType)
                .WithIdentifier(SyntaxFactory.Identifier("ex")))
            .WithBlock(SyntaxFactory.Block());

        var tryCatchStatement = SyntaxFactory.TryStatement()
            .WithBlock(tryBlock)
            .WithCatches(SyntaxFactory.SingletonList(catchClause));

        var newRoot = root.ReplaceNode(statement, tryCatchStatement.WithAdditionalAnnotations(Formatter.Annotation).NormalizeWhitespace(elasticTrivia: true));

        return document.WithSyntaxRoot(newRoot);
    }
}
