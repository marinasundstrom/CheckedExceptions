using System.Text;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace CheckedExceptions;

public static class XmlDocumentationHelper
{
    public static Dictionary<string, XElement> CreateMemberLookup(XDocument xmlDoc)
    {
        // Query the <member> elements
        var members = xmlDoc.Descendants("member");

        // Build the lookup
        var lookup = members
            .Where(m => m.Attribute("name") != null) // Ensure the member has a 'name' attribute
            .ToDictionary(
                m => m.Attribute("name").Value,        // Key: the member's name attribute
                m => m                   // Value: the inner XML or text content
            );

        return lookup;
    }

    public static string Mangle(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol typeSymbol:
                return MangleNamedType(typeSymbol);

            case IMethodSymbol methodSymbol:
                return MangleMethod(methodSymbol);

            case IPropertySymbol propertySymbol:
                return MangleProperty(propertySymbol);

            case IFieldSymbol fieldSymbol:
                return MangleField(fieldSymbol);

            case IEventSymbol eventSymbol:
                return MangleEvent(eventSymbol);

            default:
                throw new NotSupportedException($"Unsupported symbol type: {symbol.GetType()}");
        }
    }

    private static string MangleNamedType(INamedTypeSymbol typeSymbol)
    {
        var prefix = "T:";
        var fullName = GetFullTypeName(typeSymbol);
        return $"{prefix}{fullName}";
    }

    private static string MangleMethod(IMethodSymbol methodSymbol)
    {
        var prefix = "M:";
        var fullName = GetFullTypeName(methodSymbol.ContainingType);
        var methodName = methodSymbol.Name;

        // Append type parameters for generic methods
        if (methodSymbol.TypeParameters.Length > 0)
        {
            methodName += $"``{methodSymbol.TypeParameters.Length}";
        }

        // Append parameter types
        var parameterTypes = methodSymbol.Parameters
            .Select(p => GetFullTypeName(p.Type))
            .ToArray();

        var parameterString = parameterTypes.Length > 0
            ? $"({string.Join(",", parameterTypes)})"
            : string.Empty;

        return $"{prefix}{fullName}.{methodName}{parameterString}";
    }

    private static string MangleProperty(IPropertySymbol propertySymbol)
    {
        var prefix = "P:";
        var fullName = GetFullTypeName(propertySymbol.ContainingType);
        return $"{prefix}{fullName}.{propertySymbol.Name}";
    }

    private static string MangleField(IFieldSymbol fieldSymbol)
    {
        var prefix = "F:";
        var fullName = GetFullTypeName(fieldSymbol.ContainingType);
        return $"{prefix}{fullName}.{fieldSymbol.Name}";
    }

    private static string MangleEvent(IEventSymbol eventSymbol)
    {
        var prefix = "E:";
        var fullName = GetFullTypeName(eventSymbol.ContainingType);
        return $"{prefix}{fullName}.{eventSymbol.Name}";
    }

    private static string GetFullTypeName(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            // Handle generic type parameters
            var name = namedTypeSymbol.Name;
            if (namedTypeSymbol.TypeParameters.Length > 0)
            {
                name += $"`{namedTypeSymbol.TypeParameters.Length}";
            }

            // Recursively get the containing type's name for nested types
            if (namedTypeSymbol.ContainingType != null)
            {
                return $"{GetFullTypeName(namedTypeSymbol.ContainingType)}+{name}";
            }

            // Include namespace
            if (!string.IsNullOrEmpty(namedTypeSymbol.ContainingNamespace?.ToString()))
            {
                return $"{namedTypeSymbol.ContainingNamespace}.{name}";
            }

            return name;
        }

        // Handle primitive types and arrays
        return typeSymbol.ToDisplayString();
    }
}
