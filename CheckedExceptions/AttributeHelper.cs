namespace Sundstrom.CheckedExceptions;

using System.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class AttributeHelper
{
    public static AttributeData? GetSpecificAttributeData(AttributeSyntax? attributeSyntax, SemanticModel? semanticModel)
    {
        if (attributeSyntax is null || semanticModel is null)
            return null;

        // Get the symbol to which the attribute is applied
        Debug.Assert(attributeSyntax.Parent?.Parent is not null);
        var declaredSymbol = semanticModel.GetDeclaredSymbol(attributeSyntax.Parent?.Parent!);

        if (declaredSymbol is null)
            return null;

        // Get all attributes applied to the symbol
        var attributes = declaredSymbol.GetAttributes();

        // Find the matching AttributeData
        return attributes.FirstOrDefault(attr =>
            attr.ApplicationSyntaxReference?.SyntaxTree == attributeSyntax.SyntaxTree &&
            attr.ApplicationSyntaxReference?.Span == attributeSyntax.Span);
    }
}