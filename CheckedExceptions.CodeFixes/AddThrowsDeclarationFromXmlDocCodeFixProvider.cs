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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddThrowsDeclarationFromXmlDocCodeFixProvider)), Shared]
public class AddThrowsDeclarationFromXmlDocCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add throws declaration from XML doc";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows];

    public sealed override FixAllProvider GetFixAllProvider() =>
        null!; //WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        var node = root.FindNode(diagnostics.First().Location.SourceSpan);

        SyntaxNode? method = null;

        if (node is GlobalStatementSyntax globalStatement)
        {
            method = globalStatement.Statement as LocalFunctionStatementSyntax;
        }
        else
        {
            method = (SyntaxNode?)node.FirstAncestorOrSelf<MethodDeclarationSyntax>()
                ?? node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        }

        if (method is null)
            return;

        var diagnosticsCount = diagnostics.Length;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnosticsCount > 1
                    ? Title.Replace("declaration", "declarations")
                    : Title,
                createChangedDocument: c => ApplyCodefix(context.Document, method, diagnostics, c),
                equivalenceKey: Title),
            diagnostics);
    }

    private static async Task<Document> ApplyCodefix(Document document, SyntaxNode method, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        // Collect exception type names from diagnostics
        var exceptionsToAdd = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var ex) ? ex : null)
            .Where(x => x is not null)
            .Distinct()
            .ToList();

        // Try to find an existing [Throws]
        var firstThrowsAttribute = method.ChildNodes()
            .OfType<AttributeListSyntax>()
            .SelectMany(l => l.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Throws" or "ThrowsAttribute");

        var newMethod = method;

        if (firstThrowsAttribute != null)
        {
            var newArgs = firstThrowsAttribute.ArgumentList.Arguments.AddRange(
                exceptionsToAdd.Select(ex =>
                    AttributeArgument(TypeOfExpression(ParseTypeName(ex!)))));

            var updatedThrows = firstThrowsAttribute.WithArgumentList(
                firstThrowsAttribute.ArgumentList.WithArguments(newArgs));

            newMethod = method.ReplaceNode(firstThrowsAttribute, updatedThrows);
        }
        else
        {
            var args = SeparatedList(exceptionsToAdd.Select(ex =>
                AttributeArgument(TypeOfExpression(ParseTypeName(ex!)))));

            var attribute = Attribute(
                IdentifierName("Throws"),
                AttributeArgumentList(args));

            var leadingTrivia = method.GetLeadingTrivia();

            if (method is MethodDeclarationSyntax methodDeclaration)
            {
                newMethod = methodDeclaration
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }
            else if (method is LocalFunctionStatementSyntax localFunctionStatement)
            {
                newMethod = localFunctionStatement
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(
                    AttributeList(SingletonSeparatedList(attribute)));
            }

            newMethod = newMethod.WithLeadingTrivia(leadingTrivia);
        }

        //newMethod = newMethod.WithAdditionalAnnotations(Formatter.Annotation);
        var newRoot = root.ReplaceNode(method, newMethod);

        return document.WithSyntaxRoot(newRoot);
    }
}