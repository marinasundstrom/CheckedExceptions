namespace Sundstrom.CheckedExceptions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class AttributeHelper
{
    public static AttributeData? GetSpecificAttributeData(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        if (attributeSyntax == null || semanticModel == null)
            return null;

        // Get the symbol to which the attribute is applied
        var declaredSymbol = semanticModel.GetDeclaredSymbol(attributeSyntax.Parent?.Parent);

        if (declaredSymbol == null)
            return null;

        // Get all attributes applied to the symbol
        var attributes = declaredSymbol.GetAttributes();

        // Find the matching AttributeData
        return attributes.FirstOrDefault(attr =>
            attr.ApplicationSyntaxReference?.SyntaxTree == attributeSyntax.SyntaxTree &&
            attr.ApplicationSyntaxReference?.Span == attributeSyntax.Span);
    }
}