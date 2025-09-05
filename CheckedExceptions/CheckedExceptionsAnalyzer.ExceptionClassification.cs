using Microsoft.CodeAnalysis;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    public static ExceptionClassification GetExceptionClassification(
        INamedTypeSymbol exceptionType,
        AnalyzerSettings settings)
    {
        var exceptionName = exceptionType.ToDisplayString();

        if (settings.Exceptions.TryGetValue(exceptionName, out var classification))
        {
            return classification;
        }

        return ExceptionClassification.Strict;
    }

    public static bool ShouldIncludeException(
        INamedTypeSymbol exceptionType,
        AnalyzerSettings settings)
        => GetExceptionClassification(exceptionType, settings) != ExceptionClassification.Ignored;
}

