using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MaterializeEnumerableCodeFixProvider)), Shared]
public class MaterializeEnumerableCodeFixProvider : CodeFixProvider
{
    private const string Title = "Materialize enumeration with ToArray";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdDeferredMustBeHandled];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var node = GetExpression(root, diagnostic);
        if (node is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                c => AddToArrayAsync(context.Document, diagnostic, c),
                Title),
            context.Diagnostics);
    }

    private static async Task<Document> AddToArrayAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
        if (root is null)
            return document;

        var expression = GetExpression(root, diagnostic);
        if (expression is null)
            return document;

        var exprWithoutTrivia = expression.WithoutTrivia();
        var target = exprWithoutTrivia is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            ? exprWithoutTrivia
            : ParenthesizedExpression(exprWithoutTrivia);

        var newExpression = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                target,
                IdentifierName("ToArray")),
            ArgumentList())
            .WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode((SyntaxNode)expression, newExpression);

        if (!newRoot.Usings.Any(u => u.Name is IdentifierNameSyntax id && id.Identifier.Text == "System.Linq" ||
                                     u.Name is QualifiedNameSyntax q && q.ToString() == "System.Linq"))
        {
            newRoot = newRoot.AddUsings(UsingDirective(IdentifierName("System.Linq")));
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax? GetExpression(SyntaxNode? root, Diagnostic diagnostic)
        => root?.FindNode(diagnostic.Location.SourceSpan) as ExpressionSyntax;
}
