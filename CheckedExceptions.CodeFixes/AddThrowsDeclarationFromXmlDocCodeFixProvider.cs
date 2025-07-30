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
        WellKnownFixAllProviders.BatchFixer;

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

        // Collect exception type names from diagnostics
        var toAdd = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var ex) ? ex : null)
            .Where(x => x is not null)
            .Distinct()
            .ToList();

        if (toAdd.Count == 0)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnostics.Length > 1
                    ? Title.Replace("declaration", "declarations")
                    : Title,
                createChangedDocument: async c => ApplyCodefix(context, root, method, toAdd),
                equivalenceKey: Title),
            diagnostics);
    }

    private static Document ApplyCodefix(CodeFixContext context, SyntaxNode root, SyntaxNode method, List<string?> toAdd)
    {
        var newMethod = method;

        // Try to find an existing [Throws]
        var firstThrows = method.ChildNodes()
            .OfType<AttributeListSyntax>()
            .SelectMany(l => l.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Throws" or "ThrowsAttribute");

        if (firstThrows != null)
        {
            var newArgs = firstThrows.ArgumentList.Arguments.AddRange(
                toAdd.Select(ex =>
                    AttributeArgument(TypeOfExpression(ParseTypeName(ex!)))));

            var updatedThrows = firstThrows.WithArgumentList(
                firstThrows.ArgumentList.WithArguments(newArgs));

            newMethod = method.ReplaceNode(firstThrows, updatedThrows);
        }
        else
        {
            var args = SeparatedList(toAdd.Select(ex =>
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

        return context.Document.WithSyntaxRoot(newRoot);
    }
}