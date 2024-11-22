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
        var diagnostics = context.Diagnostics;

        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = root.FindNode(diagnostics.First().Location.SourceSpan);

        // Register code fixes
        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddThrowsAttribute,
                createChangedDocument: c => AddThrowsAttributeAsync(context.Document, node, diagnostics, c),
                equivalenceKey: TitleAddThrowsAttribute),
            diagnostics);
    }

    private async Task<Document> AddThrowsAttributeAsync(Document document, SyntaxNode node, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        // Attempt to retrieve the exception type from diagnostic arguments
        var exceptionTypeNames = diagnostics.Select(diagnostic => diagnostic.Properties["ExceptionType"]);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        var ancestor = GetContainingMethodOrConstruct(node);

        if (ancestor == null)
            return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        List<AttributeSyntax> attributes = new List<AttributeSyntax>();

        foreach (var exceptionTypeName in exceptionTypeNames.Distinct())
        {
            var exceptionType = SyntaxFactory.ParseTypeName(exceptionTypeName);

            if (exceptionType == null)
                return document;

            if (ancestor is BaseMethodDeclarationSyntax m && HasThrowsAttribute(m, exceptionType))
                continue;

            if (ancestor is LambdaExpressionSyntax l && HasThrowsAttribute(l, exceptionType))
                continue;

            if (ancestor is LocalFunctionStatementSyntax lf && HasThrowsAttribute(lf, exceptionType))
                continue;

            var attributeSyntax = SyntaxFactory.Attribute(SyntaxFactory.ParseName("Throws"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.TypeOfExpression(
                       exceptionType)));

            attributes.Add(attributeSyntax);
        }

        var lineEndingTrivia = root.DescendantTrivia().FirstOrDefault(trivia =>
            trivia.IsKind(SyntaxKind.EndOfLineTrivia));

        if (lineEndingTrivia == default)
        {
            lineEndingTrivia = SyntaxFactory.CarriageReturnLineFeed;
        }

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes))
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
                .WithAdditionalAnnotations(Formatter.Annotation)
                .NormalizeWhitespace(elasticTrivia: true));
            return editor.GetChangedDocument();
        }

        return document;
    }

    private bool HasThrowsAttribute(BaseMethodDeclarationSyntax methodDeclaration, TypeSyntax exceptionType)
    {
        var attributes = methodDeclaration.AttributeLists.FirstOrDefault()?.Attributes;

        if (attributes is null)
            return false;

        return CheckHasExceptionType(exceptionType, attributes);
    }

    private bool HasThrowsAttribute(LambdaExpressionSyntax lambdaExpression, TypeSyntax exceptionType)
    {
        var attributes = lambdaExpression.AttributeLists.FirstOrDefault()?.Attributes;

        if (attributes is null)
            return false;

        return CheckHasExceptionType(exceptionType, attributes);
    }

    private bool HasThrowsAttribute(LocalFunctionStatementSyntax localFunction, TypeSyntax exceptionType)
    {
        var attributes = localFunction.AttributeLists.FirstOrDefault()?.Attributes;

        if (attributes is null)
            return false;

        return CheckHasExceptionType(exceptionType, attributes);
    }

    private static bool CheckHasExceptionType(TypeSyntax exceptionType, SeparatedSyntaxList<AttributeSyntax>? attributes)
    {
        return attributes.Value
            .Where(x => x.Name.ToString() == "ThrowsAttribute")
            .Any(x => x.ArgumentList
                .Arguments.Any(x => x.Expression is TypeOfExpressionSyntax z && z.Type == exceptionType));
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
