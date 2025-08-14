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

    private static IEnumerable<INamedTypeSymbol> GetKnownMethodExceptions(IMethodSymbol method, Compilation c)
    {
        // Key by fully-qualified containing type + method name + arity we care about.
        // Use parameter shapes to disambiguate common overloads.
        var typeName = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = method.Name;

        // int.Parse(...)
        if (typeName == "global::System.Int32" && name == "Parse")
            return Types(c, "System.FormatException", "System.OverflowException");

        // long.Parse, short.Parse, byte.Parse, etc.
        if (typeName.StartsWith("global::System.") && name == "Parse" && IsIntegral(method.ContainingType.SpecialType))
            return Types(c, "System.FormatException", "System.OverflowException");

        // double/float/decimal.Parse(...)
        if (typeName is "global::System.Double" or "global::System.Single" or "global::System.Decimal")
            if (name == "Parse")
                return Types(c, "System.FormatException", "System.OverflowException");

        // Convert.ToInt32(string), etc. (Format + Overflow)
        if (typeName == "global::System.Convert" && name.StartsWith("To"))
            if (method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_String)
                return Types(c, "System.FormatException", "System.OverflowException");

        // default
        return Array.Empty<INamedTypeSymbol>();

        static bool IsIntegral(SpecialType st) =>
            st is SpecialType.System_Byte or SpecialType.System_SByte
               or SpecialType.System_Int16 or SpecialType.System_UInt16
               or SpecialType.System_Int32 or SpecialType.System_UInt32
               or SpecialType.System_Int64 or SpecialType.System_UInt64;

        static IEnumerable<INamedTypeSymbol> Types(Compilation comp, params string[] metadataNames)
            => metadataNames.Select(n => comp.GetTypeByMetadataName(n)).Where(t => t is not null)!;
    }
}