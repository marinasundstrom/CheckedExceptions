using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddThrowsAttributeCodeFixProvider)), Shared]
public class AddThrowsAttributeCodeFixProvider : CodeFixProvider
{
    private const string TitleAddThrowsAttribute = "Add throws declaration";

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

        // This fix is not applicable to top-level statements
        if (node.IsInTopLevelStatement())
            return;

        var diagnosticsCount = diagnostics.Length;

        // Register code fixes
        context.RegisterCodeFix(
        CodeAction.Create(
            title: diagnosticsCount > 1 ? TitleAddThrowsAttribute.Replace("declaration", "declarations") : TitleAddThrowsAttribute,
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

        if (ancestor is null)
            return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        List<AttributeSyntax> attributes = new List<AttributeSyntax>();

        var currentThrowsAttributes = GetThrowsAttributes(ancestor);

        var throwsAttributeSyntax = currentThrowsAttributes.FirstOrDefault();

        var existingThrowsAttributeSyntax = throwsAttributeSyntax;
        var existingAttributeList = throwsAttributeSyntax?.Parent as AttributeListSyntax;

        foreach (var exceptionTypeName in exceptionTypeNames.Distinct())
        {
            var exceptionType = ParseTypeName(exceptionTypeName);

            if (exceptionType is null)
                return document;

            if (CheckHasExceptionType(exceptionType, currentThrowsAttributes))
                continue;

            if (throwsAttributeSyntax is null)
            {
                throwsAttributeSyntax = Attribute(ParseName("Throws"))
                .AddArgumentListArguments(
                    AttributeArgument(
                        TypeOfExpression(
                           exceptionType)));
            }
            else
            {
                var newArgList = throwsAttributeSyntax.ArgumentList.Arguments.Add(AttributeArgument(
                        TypeOfExpression(
                           exceptionType))
                    .WithLeadingTrivia(Whitespace(" ")));

                throwsAttributeSyntax = throwsAttributeSyntax
                    .WithArgumentList(AttributeArgumentList(newArgList));
            }
        }

        var attributeList = existingAttributeList is not null
            ? existingAttributeList!.ReplaceNode(existingThrowsAttributeSyntax, throwsAttributeSyntax)
            : AttributeList([throwsAttributeSyntax]);

        SyntaxNode newAncestor = null;

        if (ancestor is MethodDeclarationSyntax methodDeclaration)
        {
            methodDeclaration = existingAttributeList is not null
                ? methodDeclaration.ReplaceNode(existingAttributeList!, attributeList)
                : methodDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(attributeList);

            newAncestor = methodDeclaration
                    .WithLeadingTrivia(ancestor.GetLeadingTrivia())
                    .WithTrailingTrivia(ancestor.GetTrailingTrivia());
        }
        else if (ancestor is ConstructorDeclarationSyntax constructorDeclaration)
        {
            constructorDeclaration = existingAttributeList is not null
                ? constructorDeclaration.ReplaceNode(existingAttributeList!, attributeList)
                : constructorDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(attributeList);

            newAncestor = constructorDeclaration
                    .WithLeadingTrivia(ancestor.GetLeadingTrivia())
                    .WithTrailingTrivia(ancestor.GetTrailingTrivia());
        }
        else if (ancestor is AccessorDeclarationSyntax accessorDeclaration)
        {
            accessorDeclaration = existingAttributeList is not null
                ? accessorDeclaration.ReplaceNode(existingAttributeList!, attributeList)
                : accessorDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(attributeList);

            newAncestor = accessorDeclaration
                    .WithLeadingTrivia(ancestor.GetLeadingTrivia())
                    .WithTrailingTrivia(ancestor.GetTrailingTrivia());
        }
        else if (ancestor is LocalFunctionStatementSyntax localFunction)
        {
            localFunction = localFunction.WithoutLeadingTrivia();

            localFunction = existingAttributeList is not null
                ? localFunction.ReplaceNode(existingAttributeList!, attributeList)
                : localFunction.AddAttributeLists(attributeList);

            newAncestor = localFunction
                    .WithLeadingTrivia(ancestor.GetLeadingTrivia())
                    .WithTrailingTrivia(ancestor.GetTrailingTrivia());
        }
        else if (ancestor is LambdaExpressionSyntax lambdaExpression)
        {
            lambdaExpression = existingAttributeList is not null
                ? lambdaExpression.ReplaceNode(existingAttributeList!, attributeList)
                : lambdaExpression.AddAttributeLists(attributeList);

            newAncestor = lambdaExpression
                    .WithLeadingTrivia(ancestor.GetLeadingTrivia())
                    .WithTrailingTrivia(ancestor.GetTrailingTrivia());
        }

        if (newAncestor is not null)
        {
            editor.ReplaceNode(ancestor, newAncestor.WithAdditionalAnnotations(Formatter.Annotation));
            return editor.GetChangedDocument();
        }

        return document;
    }

    private IEnumerable<AttributeSyntax>? GetThrowsAttributes(SyntaxNode ancestor)
    {
        return ancestor switch
        {
            BaseMethodDeclarationSyntax m => GetThrowsAttributes(m),
            LambdaExpressionSyntax l => GetThrowsAttributes(l),
            LocalFunctionStatementSyntax lf => GetThrowsAttributes(lf),
            AccessorDeclarationSyntax a => GetThrowsAttributes(a),
            _ => throw new InvalidOperationException()
        };
    }

    private IEnumerable<AttributeSyntax> GetThrowsAttributes(BaseMethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute");
    }

    private IEnumerable<AttributeSyntax> GetThrowsAttributes(LambdaExpressionSyntax lambdaExpression)
    {
        return lambdaExpression.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute");
    }

    private IEnumerable<AttributeSyntax> GetThrowsAttributes(LocalFunctionStatementSyntax localFunction)
    {
        return localFunction.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute");
    }

    private IEnumerable<AttributeSyntax> GetThrowsAttributes(AccessorDeclarationSyntax accessorDeclaration)
    {
        return accessorDeclaration.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute");
    }

    private static bool CheckHasExceptionType(TypeSyntax exceptionType, IEnumerable<AttributeSyntax>? attributes)
    {
        return attributes
            .Where(x => x.Name.ToString() is "Throws" or "ThrowsAttribute")
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