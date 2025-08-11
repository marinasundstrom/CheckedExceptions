using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static bool ShouldIgnore(SyntaxNode node, ExceptionMode mode)
    {
        if (mode is ExceptionMode.Always)
            return true;

        if (mode is ExceptionMode.Throw && node is ThrowStatementSyntax or ThrowExpressionSyntax)
            return true;

        if (mode is ExceptionMode.Propagation && node
            is MemberAccessExpressionSyntax
            or IdentifierNameSyntax
            or InvocationExpressionSyntax)
            return true;

        return false;
    }

    public static bool ShouldIncludeException(INamedTypeSymbol exceptionType, SyntaxNode node, AnalyzerSettings settings)
    {
        var exceptionName = exceptionType.ToDisplayString();

        if (FilterIgnored(settings, exceptionName))
        {
            // Completely ignore this exception
            return false;
        }
        else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
        {
            if (ShouldIgnore(node, mode))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FilterIgnored(AnalyzerSettings settings, string exceptionName)
    {
        bool matchedPositive = false;

        // First pass: check negations
        foreach (var pattern in settings.IgnoredExceptions)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern.StartsWith("!"))
            {
                var negated = pattern.Substring(1);
                if (IsMatch(exceptionName, negated))
                {
                    // Explicitly not ignored -> wins immediately
                    return false;
                }
            }
        }

        // Second pass: check positive patterns
        foreach (var pattern in settings.IgnoredExceptions)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (!pattern.StartsWith("!"))
            {
                if (IsMatch(exceptionName, pattern))
                {
                    matchedPositive = true;
                }
            }
        }

        return matchedPositive;
    }

    private static bool IsMatch(string exceptionName, string pattern)
    {
        // Wildcard '*' support
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(exceptionName, regexPattern);
        }

        // Exact match
        return string.Equals(exceptionName, pattern, StringComparison.Ordinal);
    }
}