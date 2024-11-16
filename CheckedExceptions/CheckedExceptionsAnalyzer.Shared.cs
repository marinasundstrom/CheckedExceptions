using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// Retrieves the name of the exception type from a ThrowsAttribute's AttributeData.
    /// </summary>
    private string GetExceptionTypeName(AttributeData attributeData)
    {
        if (attributeData == null)
            return string.Empty;

        // Ensure the attribute is ThrowsAttribute
        if (attributeData.AttributeClass?.Name != "ThrowsAttribute")
            return string.Empty;

        // Ensure there's at least one constructor argument
        if (attributeData.ConstructorArguments.Length == 0)
            return string.Empty;

        // The first constructor argument should be the exception type (typeof(Foo))
        var exceptionTypeArg = attributeData.ConstructorArguments[0];

        // The argument is of type System.Type, represented as a TypeOf expression
        if (exceptionTypeArg.Value is INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol.Name;
        }

        // If not directly a named type, attempt to get the type from the type argument
        if (exceptionTypeArg.Kind == TypedConstantKind.Type && exceptionTypeArg.Value is ITypeSymbol typeSymbol)
        {
            return typeSymbol.Name;
        }

        return string.Empty;
    }

    /// <summary>
    /// Retrieves the name of the exception type from a ThrowsAttribute's AttributeSyntax.
    /// </summary>
    private string GetExceptionTypeName(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        // Ensure the attribute is ThrowsAttribute
        var attributeType = semanticModel.GetTypeInfo(attributeSyntax).Type;
        if (attributeType == null || attributeType.Name != "ThrowsAttribute")
            return string.Empty;

        // Ensure there is at least one argument
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList == null || argumentList.Arguments.Count == 0)
            return string.Empty;

        var firstArg = argumentList.Arguments[0];
        var expr = firstArg.Expression;

        // Check if it's a typeof expression
        if (expr is TypeOfExpressionSyntax typeOfExpr)
        {
            var typeSyntax = typeOfExpr.Type;
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            var typeSymbol = typeInfo.Type as INamedTypeSymbol;
            if (typeSymbol != null)
            {
                return typeSymbol.Name;
            }
        }
        else
        {
            // Handle other possible expressions if necessary
            // For example, directly passing a Type variable, which is uncommon for attributes
        }

        return string.Empty;
    }
}