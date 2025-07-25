
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

public static class CatchClauseUtils
{
    public static List<CatchClauseSyntax> CreateCatchClauses(IEnumerable<string> exceptionTypeNames, int level = 0)
    {
        List<CatchClauseSyntax> catchClauses = new List<CatchClauseSyntax>();

        foreach (var exceptionTypeName in exceptionTypeNames.Distinct())
        {
            if (string.IsNullOrWhiteSpace(exceptionTypeName))
                continue;

            string catchExceptionVariableName = CreateVariableName(exceptionTypeName);

            var exceptionType = ParseTypeName(exceptionTypeName);

            var catchClause = CatchClause()
                .WithDeclaration(
                    CatchDeclaration(exceptionType)
                    .WithIdentifier(Identifier(catchExceptionVariableName)))
                .WithBlock(Block()) // You might want to add meaningful handling here
                .WithAdditionalAnnotations(Formatter.Annotation);

            catchClauses.Add(catchClause);
        }

        return catchClauses;
    }

    private static string CreateVariableName(string exceptionTypeName)
    {
        if (exceptionTypeName is "Exception" or "System.Exception")
        {
            return "ex";
        }

        // Get the simple type name (strip namespace if present)
        var simpleName = exceptionTypeName.Contains('.')
        ? exceptionTypeName.Substring(exceptionTypeName.LastIndexOf('.') + 1)
        : exceptionTypeName;

        // Convert to camelCase
        if (string.IsNullOrEmpty(simpleName) || char.IsLower(simpleName[0]))
            return simpleName;

        return char.ToLower(simpleName[0]) + simpleName.Substring(1);
    }
}