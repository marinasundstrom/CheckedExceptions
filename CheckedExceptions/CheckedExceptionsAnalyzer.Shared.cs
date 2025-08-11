using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static Location GetSignificantLocation(SyntaxNode expression)
    {
        if (expression is InvocationExpressionSyntax)
            return GetSignificantInvocationLocation(expression);

        if (expression is ElementAccessExpressionSyntax)
            return GetSignificantInvocationLocation(expression);

        var node = GetSignificantNodeCore(expression);
        return node.GetLocation();
    }

    private static SyntaxNode GetSignificantNodeCore(SyntaxNode expression)
    {
        if (expression is InvocationExpressionSyntax ie)
        {
            return GetSignificantNodeCore(ie.Expression);
        }

        if (expression is ElementAccessExpressionSyntax ea)
        {
            return GetSignificantNodeCore(ea.Expression);
        }

        if (expression is MemberAccessExpressionSyntax mae)
        {
            return mae.Name;
        }

        return expression;
    }

    private static Location GetSignificantInvocationLocation(SyntaxNode expression)
    {
        if (expression is InvocationExpressionSyntax invocation)
        {
            // Get the name part (e.g., bar in foo.bar(s))
            var nameNode = GetSignificantNodeCore(invocation.Expression);

            // Compute the span from name start to the full invocation end
            var start = nameNode.SpanStart;
            var end = invocation.Span.End;

            var span = TextSpan.FromBounds(start, end);
            return Location.Create(invocation.SyntaxTree, span);
        }
        else if (expression is ElementAccessExpressionSyntax elementAccess)
        {
            // Get the name part (e.g., bar in foo.bar[2])
            var nameNode = GetSignificantNodeCore(elementAccess.Expression);

            // Compute the span from name start to the full invocation end
            var start = nameNode.SpanStart;
            var end = elementAccess.Span.End;

            var span = TextSpan.FromBounds(start, end);
            return Location.Create(elementAccess.SyntaxTree, span);
        }

        return expression.GetLocation();
    }
}