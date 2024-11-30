namespace Sundstrom.CheckedExceptions;

using Microsoft.CodeAnalysis;

/// <summary>
/// Extension methods for type symbol analysis.
/// </summary>
public static class TypeSymbolExtensions
{
    /// <summary>
    /// Determines if a type inherits from a base type.
    /// </summary>
    public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (type is null || baseType is null)
            return false;

        var current = type.BaseType;
        while (current is not null)
        {
            if (current.Equals(baseType, SymbolEqualityComparer.Default))
                return true;

            current = current.BaseType;
        }
        return false;
    }
}
