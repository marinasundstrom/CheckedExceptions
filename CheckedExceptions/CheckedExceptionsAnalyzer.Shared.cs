using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// Retrieves the name of the exception type from a ThrowsAttribute's AttributeData.
    /// </summary>
    private string GetExceptionTypeName(AttributeData attributeData)
    {
        if (attributeData is null)
            return string.Empty;

        // Ensure the attribute is ThrowsAttribute
        if (attributeData.AttributeClass?.Name is not "ThrowsAttribute")
            return string.Empty;

        // Ensure there's at least one constructor argument
        if (attributeData.ConstructorArguments.Length is 0)
            return string.Empty;

        // The first constructor argument should be the exception type (typeof(Foo))
        var exceptionTypeArg = attributeData.ConstructorArguments[0];

        // The argument is of type System.Type, represented as a TypeOf expression
        if (exceptionTypeArg.Value is INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol.Name;
        }

        // If not directly a named type, attempt to get the type from the type argument
        if (exceptionTypeArg.Kind is TypedConstantKind.Type && exceptionTypeArg.Value is ITypeSymbol typeSymbol)
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
        if (attributeType is null || attributeType.Name is not "ThrowsAttribute")
            return string.Empty;

        // Ensure there is at least one argument
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList is null || argumentList.Arguments.Count is 0)
            return string.Empty;

        var firstArg = argumentList.Arguments[0];
        var expr = firstArg.Expression;

        // Check if it's a typeof expression
        if (expr is TypeOfExpressionSyntax typeOfExpr)
        {
            var typeSyntax = typeOfExpr.Type;
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            var typeSymbol = typeInfo.Type as INamedTypeSymbol;
            if (typeSymbol is not null)
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

    /// <summary>
    /// Retrieves the name of the exception type from a ThrowsAttribute's AttributeSyntax.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> GetExceptionTypes(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        // Ensure the attribute is ThrowsAttribute
        var attributeType = semanticModel.GetTypeInfo(attributeSyntax).Type;
        if (attributeType is null || attributeType.Name is not "ThrowsAttribute")
            yield break;

        // Ensure there is at least one argument
        var argumentList = attributeSyntax.ArgumentList;

        if (argumentList is null)
            yield break;

        foreach (var args in argumentList.Arguments)
        {
            var expr = args.Expression;

            // Check if it's a typeof expression
            if (expr is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeSyntax = typeOfExpr.Type;
                var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
                var typeSymbol = typeInfo.Type as INamedTypeSymbol;
                if (typeSymbol is not null)
                {
                    yield return typeSymbol;
                }
            }
            else
            {
                // Handle other possible expressions if necessary
                // For example, directly passing a Type variable, which is uncommon for attributes
            }
        }
    }

    /// <summary>
    /// Retrieves the enclosing catch clause for a given node.
    /// </summary>
    private CatchClauseSyntax? GetEnclosingCatchClause(SyntaxNode node)
    {
        return node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
    }

    private static SyntaxNode GetExpression(SyntaxNode expression)
    {
        if (expression is InvocationExpressionSyntax ie)
        {
            return GetExpression(ie.Expression);
        }

        if (expression is MemberAccessExpressionSyntax mae)
        {
            return mae.Name;
        }

        return expression;
    }
}