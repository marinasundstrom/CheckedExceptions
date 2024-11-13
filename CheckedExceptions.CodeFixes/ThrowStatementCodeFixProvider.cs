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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThrowStatementCodeFixProvider)), Shared]
public class ThrowStatementCodeFixProvider : CodeFixProvider
{
    private const string TitleAddThrowsAttribute = "Add ThrowsAttribute";
    private const string TitleAddTryCatch = "Add try-catch block";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(ThrowStatementAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();

        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // Register code fixes
        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddThrowsAttribute,
                createChangedDocument: c => AddThrowsAttributeAsync(context.Document, node, c),
                equivalenceKey: TitleAddThrowsAttribute),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, node, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostic);
    }

    private async Task<Document> AddThrowsAttributeAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        // Find the containing method or construct
        var throwStatement = node as ThrowStatementSyntax;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var exceptionType = semanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;

        if (exceptionType == null)
            return document;

        var ancestor = GetContainingMethodOrConstruct(throwStatement);

        if (ancestor == null)
            return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var attributeSyntax = SyntaxFactory.Attribute(SyntaxFactory.ParseName("Throws"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.TypeOfExpression(
                        SyntaxFactory.ParseTypeName(exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attributeSyntax))
            .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed));

        SyntaxNode newAncestor = null;

        if (ancestor is MethodDeclarationSyntax methodDeclaration)
        {
            newAncestor = methodDeclaration.AddAttributeLists(attributeList);
        }
        else if (ancestor is AccessorDeclarationSyntax accessorDeclaration)
        {
            newAncestor = accessorDeclaration.AddAttributeLists(attributeList);
        }
        else if (ancestor is LocalFunctionStatementSyntax localFunction)
        {
            newAncestor = localFunction.AddAttributeLists(attributeList);
        }
        else if (ancestor is LambdaExpressionSyntax lambdaExpression)
        {
            newAncestor = lambdaExpression.WithAttributeLists(lambdaExpression.AttributeLists.Add(attributeList));
        }

        if (newAncestor != null)
        {
            editor.ReplaceNode(ancestor, newAncestor.WithAdditionalAnnotations(Formatter.Annotation));
            return editor.GetChangedDocument();
        }

        return document;
    }

    private async Task<Document> AddTryCatchAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var throwStatement = node as ThrowStatementSyntax;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
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

    private SyntaxNode GetContainingMethodOrConstruct(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax ||
                ancestor is AccessorDeclarationSyntax ||
                ancestor is LocalFunctionStatementSyntax ||
                ancestor is LambdaExpressionSyntax)
            {
                return ancestor;
            }
        }
        return null;
    }
}
