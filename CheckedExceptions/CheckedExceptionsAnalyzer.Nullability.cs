using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static IEnumerable<ExceptionInfo> ProcessNullable(
        Compilation compilation, SemanticModel semanticModel, SyntaxNode node, IMethodSymbol methodSymbol, IEnumerable<ExceptionInfo> exceptionInfos)
    {
        var argumentNullExceptionTypeSymbol = compilation.GetTypeByMetadataName("System.ArgumentNullException");

        var isCompilationNullableEnabled = compilation.Options.NullableContextOptions is NullableContextOptions.Enable;

        var nullableContext = semanticModel.GetNullableContext(node.SpanStart);
        var isNodeInNullableContext = nullableContext is NullableContext.Enabled;

        if (isNodeInNullableContext || isCompilationNullableEnabled)
        {
            if (methodSymbol.IsExtensionMethod)
            {
                return exceptionInfos.Where(x => !x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
            }

            if (methodSymbol.Parameters.Count() is 1)
            {
                var p = methodSymbol.Parameters.First();

                if (p.NullableAnnotation is NullableAnnotation.NotAnnotated)
                {
                    return exceptionInfos.Where(x => !x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
                }
            }
            else
            {
                exceptionInfos = exceptionInfos.Where(x =>
                {
                    var p = methodSymbol.Parameters.FirstOrDefault(p => x.Parameters.Any(p2 => p.Name == p2.Name));
                    if (p is not null)
                    {
                        if (x.ExceptionType.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default)
                        && p.NullableAnnotation is NullableAnnotation.NotAnnotated)
                        {
                            return false;
                        }
                    }
                    return true;
                }).ToList();
            }
        }

        return exceptionInfos;
    }

    private static IEnumerable<INamedTypeSymbol> ProcessNullable(Compilation compilation, SemanticModel semanticModel, SyntaxNode node, IMethodSymbol methodSymbol, IEnumerable<INamedTypeSymbol> exceptions)
    {
        var argumentNullExceptionTypeSymbol = compilation.GetTypeByMetadataName("System.ArgumentNullException");

        var isCompilationNullableEnabled = compilation.Options.NullableContextOptions is NullableContextOptions.Enable;

        var nullableContext = semanticModel.GetNullableContext(node.SpanStart);
        var isNodeInNullableContext = nullableContext is NullableContext.Enabled;

        if (isNodeInNullableContext || isCompilationNullableEnabled)
        {
            return exceptions.Where(x => !x.Equals(argumentNullExceptionTypeSymbol, SymbolEqualityComparer.Default));
        }

        return exceptions;
    }
}