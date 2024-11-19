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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddThrowsAttributeCodeFixProvider)), Shared]
public class AddThrowsAttributeCodeFixProvider : CodeFixProvider
{
    private const string TitleAddThrowsAttribute = "Add ThrowsAttribute";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdUnhandled];
    public sealed override FixAllProvider GetFixAllProvider() =>
        null!; //WellKnownFixAllProviders.BatchFixer;

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
                createChangedDocument: c => AddThrowsAttributeAsync(context.Document, node, diagnostic, c),
                equivalenceKey: TitleAddThrowsAttribute),
            diagnostic);
    }

    private async Task<Document> AddThrowsAttributeAsync(Document document, SyntaxNode node, Diagnostic diagnostic, CancellationToken cancellationToken)
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

        var ancestor = GetContainingMethodOrConstruct(node);

        if (ancestor == null)
            return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var attributeSyntax = SyntaxFactory.Attribute(SyntaxFactory.ParseName("Throws"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.TypeOfExpression(
                        SyntaxFactory.ParseTypeName(exceptionTypeName))));

        var lineEndingTrivia = root.DescendantTrivia().FirstOrDefault(trivia =>
            trivia.IsKind(SyntaxKind.EndOfLineTrivia));

        if (lineEndingTrivia == default)
        {
            lineEndingTrivia = SyntaxFactory.CarriageReturnLineFeed;
        }

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attributeSyntax))
            .WithTrailingTrivia(SyntaxFactory.TriviaList(lineEndingTrivia));

        SyntaxNode newAncestor = null;

        if (ancestor is MethodDeclarationSyntax methodDeclaration)
        {
            newAncestor = methodDeclaration.AddAttributeLists(attributeList);
        }
        else if (ancestor is ConstructorDeclarationSyntax constructorDeclaration)
        {
            newAncestor = constructorDeclaration.AddAttributeLists(attributeList);
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
            editor.ReplaceNode(ancestor, newAncestor
                .WithAdditionalAnnotations(Formatter.Annotation).NormalizeWhitespace(elasticTrivia: true));
            return editor.GetChangedDocument();
        }

        return document;
    }

    private SyntaxNode GetContainingMethodOrConstruct(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax ||
                ancestor is ConstructorDeclarationSyntax ||
                ancestor is AccessorDeclarationSyntax ||
                ancestor is LocalFunctionStatementSyntax ||
                ancestor is ParenthesizedLambdaExpressionSyntax ||
                ancestor is LambdaExpressionSyntax)
            {
                return ancestor;
            }
        }
        return null;
    }
}
