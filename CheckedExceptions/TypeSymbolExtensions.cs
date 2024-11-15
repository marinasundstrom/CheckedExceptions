namespace CheckedExceptions;

using Microsoft.CodeAnalysis;

/// <summary>
/// Extension methods for type symbol analysis.
/// </summary>
public static class TypeSymbolExtensions
{
    /// <summary>
    /// Determines if a type is equal to or inherits from a base type.
    /// </summary>
    public static bool IsOrInheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (type == null || baseType == null)
            return false;

        if (type.Equals(baseType, SymbolEqualityComparer.Default))
            return true;

        return type.InheritsFrom(baseType);
    }

    /// <summary>
    /// Determines if a type inherits from a base type.
    /// </summary>
    public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Equals(baseType, SymbolEqualityComparer.Default))
                return true;

            current = current.BaseType;
        }
        return false;
    }
}
