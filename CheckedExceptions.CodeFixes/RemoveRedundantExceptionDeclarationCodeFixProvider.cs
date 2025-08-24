using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveRedundantExceptionDeclarationCodeFixProvider)), Shared]
public class RemoveRedundantExceptionDeclarationCodeFixProvider : CodeFixProvider
{
    private const string TitleRemoveRedundantExceptionDeclaration = "Remove redundant exception declaration";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdDuplicateDeclarations,
         CheckedExceptionsAnalyzer.DiagnosticIdDuplicateThrowsByHierarchy,
         CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var argument = GetArgument(root, diagnostic);

        if (argument is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleRemoveRedundantExceptionDeclaration,
                createChangedDocument: c => RemoveRedundantDeclarationAsync(context.Document, diagnostic, c),
                equivalenceKey: TitleRemoveRedundantExceptionDeclaration),
            context.Diagnostics);
    }

    private static async Task<Document> RemoveRedundantDeclarationAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var argument = GetArgument(editor.OriginalRoot, diagnostic);
        if (argument is null)
            return document;

        var attribute = (AttributeSyntax)argument.Parent.Parent;
        var list = (AttributeListSyntax)attribute.Parent;

        if (attribute.ArgumentList?.Arguments.Count == 1)
        {
            if (list.Attributes.Count == 1)
            {
                editor.RemoveNode(list);
            }
            else
            {
                editor.RemoveNode(attribute);
            }
        }
        else
        {
            var newArguments = attribute.ArgumentList.Arguments.Remove(argument);
            var newAttribute = attribute
                .WithArgumentList(attribute.ArgumentList.WithArguments(newArguments))
                .WithAdditionalAnnotations(Formatter.Annotation);
            editor.ReplaceNode(attribute, newAttribute);
        }

        return editor.GetChangedDocument();
    }

    private static AttributeArgumentSyntax? GetArgument(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        return node.FirstAncestorOrSelf<AttributeArgumentSyntax>();
    }
}
