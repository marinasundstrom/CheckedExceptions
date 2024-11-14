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
        ImmutableArray.Create(CheckedExceptionsAnalyzer.DiagnosticId2);

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
                createChangedDocument: c => AddTryCatchAsync(context.Document, node, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostic);
    }

    private async Task<Document> AddTryCatchAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var throwStatement = node as ThrowStatementSyntax;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (throwStatement.Expression == null)
            return document;

        var exceptionType = semanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;

        if (exceptionType == null)
            return document;

        // Create a try-catch block
        var tryBlock = SyntaxFactory.Block(throwStatement);

        var catchClause = SyntaxFactory.CatchClause()
            .WithDeclaration(
                SyntaxFactory.CatchDeclaration(
                    SyntaxFactory.ParseTypeName(exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
                .WithIdentifier(SyntaxFactory.Identifier("ex")))
            .WithBlock(SyntaxFactory.Block());

        var tryCatchStatement = SyntaxFactory.TryStatement()
            .WithBlock(tryBlock)
            .WithCatches(SyntaxFactory.SingletonList(catchClause));

        var newRoot = root.ReplaceNode(throwStatement, tryCatchStatement.WithAdditionalAnnotations(Formatter.Annotation));

        return document.WithSyntaxRoot(newRoot);
    }
}
