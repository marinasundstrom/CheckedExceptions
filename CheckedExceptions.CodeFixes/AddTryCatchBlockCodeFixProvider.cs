using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static CatchClauseUtils;

namespace Sundstrom.CheckedExceptions;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTryCatchBlockCodeFixProvider)), Shared]
public class AddTryCatchBlockCodeFixProvider : CodeFixProvider
{
    private const string TitleAddTryCatch = "Surround with try/catch";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [CheckedExceptionsAnalyzer.DiagnosticIdUnhandled];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostics = context.Diagnostics;
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnostic = diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        StatementSyntax? statement = node is GlobalStatementSyntax g ? g.Statement : node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddTryCatch,
                createChangedDocument: c => AddTryCatchAsync(context.Document, statement, diagnostics, c),
                equivalenceKey: TitleAddTryCatch),
            diagnostics);
    }

    private async Task<Document> AddTryCatchAsync(Document document, StatementSyntax statement, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var exceptionTypeNames = diagnostics
            .Select(d => d.Properties.TryGetValue("ExceptionType", out var type) ? type! : string.Empty);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot;

        if (statement.Parent is BlockSyntax parentBlock)
        {
            var statements = parentBlock.Statements;
            var targetIndex = statements.IndexOf(statement);
            if (targetIndex == -1) return document;

            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;
            var readSymbols = new HashSet<ISymbol>(dataFlow.ReadInside, SymbolEqualityComparer.Default);

            int start = targetIndex;
            for (int i = 0; i < targetIndex; i++)
            {
                var flow = semanticModel.AnalyzeDataFlow(statements[i])!;
                if (flow.WrittenInside.Any(readSymbols.Contains))
                {
                    start = i;
                    break;
                }
            }

            int end = targetIndex;
            var writtenSymbols = new HashSet<ISymbol>(semanticModel.AnalyzeDataFlow(statements[targetIndex])!.WrittenInside, SymbolEqualityComparer.Default);

            for (int i = targetIndex + 1; i < statements.Count; i++)
            {
                var current = statements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(current)!;

                if (currentFlow.ReadInside.Any(writtenSymbols.Contains))
                {
                    end = i;
                    writtenSymbols.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = statements
                .Skip(start).Take(end - start + 1)
                .Where(s => s is not LocalFunctionStatementSyntax)
                .ToList();

            var tryBlock = Block(statementsToWrap).WithAdditionalAnnotations(Formatter.Annotation);
            var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryCatchStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            newRoot = root.ReplaceNodes(
                statementsToWrap,
                (original, _) => original == statementsToWrap.First() ? tryCatchStatement : null!
            );
        }
        else if (statement.Parent is GlobalStatementSyntax globalStatement &&
                 root is CompilationUnitSyntax compilationUnit)
        {
            var members = compilationUnit.Members;
            var globalStatementsWithIndex = members
                .Select((m, i) => (m, i))
                .Where(t => t.m is GlobalStatementSyntax)
                .Select(t => ((GlobalStatementSyntax)t.m, t.i))
                .ToList();

            var innerStatements = globalStatementsWithIndex.Select(t => t.Item1.Statement).ToList();
            var targetIndex = innerStatements.IndexOf(statement);
            if (targetIndex == -1) return document;

            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;
            var readSymbols = new HashSet<ISymbol>(dataFlow.ReadInside, SymbolEqualityComparer.Default);

            int start = targetIndex;
            for (int i = 0; i < targetIndex; i++)
            {
                var flow = semanticModel.AnalyzeDataFlow(innerStatements[i])!;
                if (flow.WrittenInside.Any(readSymbols.Contains))
                {
                    start = i;
                    break;
                }
            }

            int end = targetIndex;
            var writtenSymbols = new HashSet<ISymbol>(semanticModel.AnalyzeDataFlow(innerStatements[targetIndex])!.WrittenInside, SymbolEqualityComparer.Default);

            for (int i = targetIndex + 1; i < innerStatements.Count; i++)
            {
                var current = innerStatements[i];
                var currentFlow = semanticModel.AnalyzeDataFlow(current)!;

                if (currentFlow.ReadInside.Any(writtenSymbols.Contains))
                {
                    end = i;
                    writtenSymbols.UnionWith(currentFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            var statementsToWrap = innerStatements
                .Skip(start).Take(end - start + 1)
                .Where(s => s is not LocalFunctionStatementSyntax)
                .ToList();

            var tryBlock = Block(statementsToWrap).WithAdditionalAnnotations(Formatter.Annotation);
            var count = globalStatement.Ancestors().OfType<TryStatementSyntax>().Count();
            var catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            var tryStatement = TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var tryGlobalStatement = GlobalStatement(tryStatement)
                .WithTriviaFrom(globalStatementsWithIndex[start].Item1)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var indicesToWrap = globalStatementsWithIndex
                .Skip(start).Take(end - start + 1)
                .Select(t => t.i).ToHashSet();

            var newMembers = new List<MemberDeclarationSyntax>();
            for (int i = 0; i < members.Count; i++)
            {
                if (i == globalStatementsWithIndex[start].i)
                {
                    newMembers.Add(tryGlobalStatement);
                }
                else if (!indicesToWrap.Contains(i))
                {
                    newMembers.Add(members[i]);
                }
            }

            newRoot = compilationUnit.WithMembers(List(newMembers));
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
