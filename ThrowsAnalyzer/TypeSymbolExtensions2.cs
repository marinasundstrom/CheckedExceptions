using System.Linq;

using Microsoft.CodeAnalysis;

namespace ThrowsAnalyzer;


// Extension method for checking type inheritance
public static class TypeSymbolExtensions2
{
    public static bool IsOrInheritsFrom2(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (type == null || baseType == null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(type, baseType))
            return true;

        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }

        return false;
    }
}
