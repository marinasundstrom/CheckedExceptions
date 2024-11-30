using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Sundstrom.CheckedExceptions
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTryCatchBlockCodeFixProvider)), Shared]
    public class AddTryCatchBlockCodeFixProvider : CodeFixProvider
    {
        private const string TitleAddTryCatch = "Add try-catch block";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostics = context.Diagnostics;

            var cancellationToken = context.CancellationToken;
            var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnostic = diagnostics.First();
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // Register the code fix only if the node is a statement or within a statement
            var statement = node.FirstAncestorOrSelf<StatementSyntax>();
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
            // Retrieve exception types from diagnostics
            var exceptionTypeNames = diagnostics
                .Select(diagnostic => diagnostic.Properties.ContainsKey("ExceptionType") ? diagnostic.Properties["ExceptionType"]! : string.Empty);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root is null)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var parentBlock = statement.Parent as BlockSyntax;
            if (parentBlock is null)
                return document; // Cannot proceed if the parent is not a block

            var statements = parentBlock.Statements;
            var targetIndex = statements.IndexOf(statement);

            if (targetIndex is -1)
                return document; // Statement not found in the parent block

            // Check if the statement is already inside a try-catch block
            var existingTryStatement = statement.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault();

            var existingCatchClause = statement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();

            if (existingTryStatement is not null && existingCatchClause is not null
                && !existingTryStatement.Catches.Contains(existingCatchClause))
            {
                // Check if any existing catch clause handles the exception types
                var existingCatchTypes = existingTryStatement.Catches
                    .SelectMany(catchClause => catchClause.Declaration is not null
                        ? new[] { catchClause.Declaration.Type.ToString() }
                        : []);

                bool alreadyHandled = exceptionTypeNames.All(etn => existingCatchTypes.Contains(etn));

                if (alreadyHandled)
                {
                    // All exception types are already handled; no need to add another try-catch
                    return document;
                }
                else
                {
                    var count2 = statement.Ancestors().OfType<TryStatementSyntax>().Count();

                    // Some exception types are not handled; add new catch clauses to the existing try-catch
                    var catchClausesToAdd = CreateCatchClauses(exceptionTypeNames.Except(existingCatchTypes), count2);

                    if (!catchClausesToAdd.Any())
                        return document; // No new catch clauses to add

                    TryStatementSyntax newTryStatement = existingTryStatement.AddCatches(catchClausesToAdd.ToArray());

                    var newRoot = root.ReplaceNode(existingTryStatement, newTryStatement.NormalizeWhitespace(elasticTrivia: true));

                    return document.WithSyntaxRoot(newRoot);
                }
            }

            // If not inside a try-catch, proceed to wrap related statements in a new try-catch

            // Perform data flow analysis on the target statement
            var dataFlow = semanticModel.AnalyzeDataFlow(statement)!;

            var variablesToTrack = new HashSet<ISymbol>(dataFlow.ReadInside.Concat(dataFlow.WrittenInside), SymbolEqualityComparer.Default);

            // Determine the range of statements to include using flow analysis
            int start = targetIndex;
            int end = targetIndex;

            // Expand upwards to include related statements
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                var currentStatement = statements[i];
                var currentDataFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentDataFlow.WrittenOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentDataFlow.ReadInside.Any(v => variablesToTrack.Contains(v)))
                {
                    start = i;
                    variablesToTrack.UnionWith(currentDataFlow.ReadInside);
                    variablesToTrack.UnionWith(currentDataFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            // Expand downwards to include related statements
            for (int i = targetIndex + 1; i < statements.Count; i++)
            {
                var currentStatement = statements[i];
                var currentDataFlow = semanticModel.AnalyzeDataFlow(currentStatement)!;

                if (currentDataFlow.ReadOutside.Any(v => variablesToTrack.Contains(v)) ||
                    currentDataFlow.WrittenInside.Any(v => variablesToTrack.Contains(v)))
                {
                    end = i;
                    variablesToTrack.UnionWith(currentDataFlow.ReadInside);
                    variablesToTrack.UnionWith(currentDataFlow.WrittenInside);
                }
                else
                {
                    break;
                }
            }

            // Extract the related statements
            var statementsToWrap = statements.Skip(start).Take(end - start + 1).ToList();

            // Create the try block
            var tryBlock = SyntaxFactory.Block(statementsToWrap)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var count = statementsToWrap.First().Ancestors().OfType<TryStatementSyntax>().Count();

            // Create catch clauses based on exception types
            List<CatchClauseSyntax> catchClauses = CreateCatchClauses(exceptionTypeNames, count);

            // Construct the try-catch statement
            TryStatementSyntax tryCatchStatement = SyntaxFactory.TryStatement()
                .WithBlock(tryBlock)
                .WithCatches(SyntaxFactory.List(catchClauses))
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Preserve leading trivia (e.g., comments) from the first statement
            tryCatchStatement = tryCatchStatement
                .WithLeadingTrivia(statementsToWrap.First().GetLeadingTrivia().Where(x => !x.IsKind(SyntaxKind.SingleLineCommentTrivia) && !x.IsKind(SyntaxKind.MultiLineCommentTrivia)))
                .WithTrailingTrivia(statementsToWrap.Last().GetTrailingTrivia());

            // Replace the original statements with the try-catch statement
            var newRootReplace = root.ReplaceNodes(
                statementsToWrap,
                (original, rewritten) => original == statementsToWrap.First() ? tryCatchStatement.NormalizeWhitespace(elasticTrivia: true) : null!
            );

            return document.WithSyntaxRoot(newRootReplace);
        }

        private static List<CatchClauseSyntax> CreateCatchClauses(IEnumerable<string> exceptionTypeNames, int level = 0)
        {
            List<CatchClauseSyntax> catchClauses = new List<CatchClauseSyntax>();

            string catchExceptionVariableName;

            if (level is 0)
            {
                catchExceptionVariableName = "ex";
            }
            else
            {
                catchExceptionVariableName = $"ex{level + 1}";
            }

            foreach (var exceptionTypeName in exceptionTypeNames.Distinct())
            {
                if (string.IsNullOrWhiteSpace(exceptionTypeName))
                    continue;

                var exceptionType = SyntaxFactory.ParseTypeName(exceptionTypeName);

                var catchClause = SyntaxFactory.CatchClause()
                    .WithDeclaration(
                        SyntaxFactory.CatchDeclaration(exceptionType)
                        .WithIdentifier(SyntaxFactory.Identifier(catchExceptionVariableName)))
                    .WithBlock(SyntaxFactory.Block()) // You might want to add meaningful handling here
                    .WithAdditionalAnnotations(Formatter.Annotation);

                catchClauses.Add(catchClause);
            }

            return catchClauses;
        }
    }
}