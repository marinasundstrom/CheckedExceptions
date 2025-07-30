using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

public static class Facts
{
    public static bool IsPotentialThrowingNode(this SyntaxNode node)
    {
        return node
            is MemberAccessExpressionSyntax
            or NameSyntax
            or InvocationExpressionSyntax
            or ThrowStatementSyntax
            or ThrowExpressionSyntax;

        // Todo add more if necessary
    }
}