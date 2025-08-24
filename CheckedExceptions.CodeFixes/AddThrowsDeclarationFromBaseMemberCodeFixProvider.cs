using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddThrowsDeclarationFromBaseMemberCodeFixProvider)), Shared]
public class AddThrowsDeclarationFromBaseMemberCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add throws declaration from base member";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsFromBaseMember];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        var targetNode = GetTargetNode(root, diagnostics.First());

        if (targetNode is null)
            return;

        var diagnosticsCount = diagnostics.Length;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnosticsCount > 1
                    ? Title.Replace("declaration", "declarations")
                    : Title,
                createChangedDocument: c => ApplyCodefix(context.Document, diagnostics, c),
                equivalenceKey: Title),
            diagnostics);
    }

    private static async Task<Document> ApplyCodefix(Document document, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
            return document;

        var targetNode = GetTargetNode(root, diagnostics.First());

        if (targetNode is null)
            return document;

        // Collect exception type names from diagnostics
        var exceptionsToAdd = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var ex) ? ex : null)
            .Where(x => x is not null)
            .Distinct()
            .ToList();

        // Try to find an existing [Throws]
        var firstThrowsAttribute = targetNode.ChildNodes()
            .OfType<AttributeListSyntax>()
            .SelectMany(l => l.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Throws" or "ThrowsAttribute");

        var newNode = targetNode;

        if (firstThrowsAttribute is not null)
        {
            var newArgs = firstThrowsAttribute.ArgumentList.Arguments.AddRange(
                exceptionsToAdd.Select(ex =>
                    AttributeArgument(TypeOfExpression(ParseTypeName(ex!)))));

            var updatedThrows = firstThrowsAttribute.WithArgumentList(
                firstThrowsAttribute.ArgumentList.WithArguments(newArgs));

            newNode = targetNode.ReplaceNode(firstThrowsAttribute, updatedThrows);
        }
        else
        {
            var args = SeparatedList(exceptionsToAdd.Select(ex =>
                AttributeArgument(TypeOfExpression(ParseTypeName(ex!)))));

            var attribute = Attribute(
                IdentifierName("Throws"),
                AttributeArgumentList(args));

            var leadingTrivia = targetNode.GetLeadingTrivia();

            if (targetNode is BaseMethodDeclarationSyntax methodDeclaration)
            {
                newNode = methodDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }
            else if (targetNode is PropertyDeclarationSyntax propertyDeclaration)
            {
                newNode = propertyDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }
            else if (targetNode is AccessorDeclarationSyntax accessorDeclaration)
            {
                newNode = accessorDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }
            else if (targetNode is LocalFunctionStatementSyntax localFunctionStatement)
            {
                newNode = localFunctionStatement
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }

            newNode = newNode.WithLeadingTrivia(leadingTrivia);
        }

        //newMethod = newMethod.WithAdditionalAnnotations(Formatter.Annotation);
        var newRoot = root.ReplaceNode(targetNode, newNode);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode? GetTargetNode(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is GlobalStatementSyntax globalStatement)
        {
            return globalStatement.Statement as LocalFunctionStatementSyntax;
        }
        else if (node is PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration;
        }
        else if (node is AccessorDeclarationSyntax accessorDeclaration)
        {
            return accessorDeclaration;
        }

        return (SyntaxNode?)node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()
            ?? node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
    }
}