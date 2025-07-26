using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

public static class SyntaxNodeExtensions
{
    public static bool IsInTopLevelStatement(this SyntaxNode node)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AccessorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case AnonymousFunctionExpressionSyntax:
                    return false;
                case GlobalStatementSyntax:
                    return true;
            }
        }

        return false;
    }
}