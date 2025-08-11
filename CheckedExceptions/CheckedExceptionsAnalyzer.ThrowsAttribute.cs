using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static List<AttributeData> GetThrowsAttributes(ISymbol symbol)
    {
        return GetThrowsAttributes(symbol.GetAttributes());
    }

    private static List<AttributeData> GetThrowsAttributes(IEnumerable<AttributeData> attributes)
    {
        return attributes
                    .Where(attr => attr.AttributeClass?.Name is "ThrowsAttribute")
                    .ToList();
    }

    /// <summary>
    /// Determines whether the given attribute is a ThrowsAttribute.
    /// </summary>
    private static bool IsThrowsAttribute(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        var attributeSymbol = semanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
        if (attributeSymbol is null)
            return false;

        var attributeType = attributeSymbol.ContainingType;
        return attributeType.Name is "ThrowsAttribute";
    }

    /// <summary>
    /// Retrieves the name of the exception type from a ThrowsAttribute's AttributeSyntax.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetExceptionTypes(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
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

    public static IEnumerable<INamedTypeSymbol> GetExceptionTypes(params IEnumerable<AttributeData> exceptionAttributes)
    {
        var constructorArguments = exceptionAttributes
            .SelectMany(attr => attr.ConstructorArguments);

        foreach (var arg in constructorArguments)
        {
            if (arg.Kind is TypedConstantKind.Array)
            {
                foreach (var t in arg.Values)
                {
                    if (t.Kind is TypedConstantKind.Type)
                    {
                        yield return (INamedTypeSymbol)t.Value!;
                    }
                }
            }
            else if (arg.Kind is TypedConstantKind.Type)
            {
                yield return (INamedTypeSymbol)arg.Value!;
            }
        }
    }

    public static IEnumerable<INamedTypeSymbol> GetDistictExceptionTypes(params IEnumerable<AttributeData> exceptionAttributes)
    {
        var exceptionTypes = GetExceptionTypes(exceptionAttributes);

        return exceptionTypes.Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>();
    }

    private static bool IsExceptionDeclaredInMember(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        return IsExceptionDeclaredInMember(context.SemanticModel, node, exceptionType);
    }

    private static bool IsExceptionDeclaredInMember(SemanticModel semanticModel, SyntaxNode node, INamedTypeSymbol exceptionType)
    {
        foreach (var ancestor in node.Ancestors())
        {
            ISymbol? symbol = null;

            switch (ancestor)
            {
                case BaseMethodDeclarationSyntax methodDeclaration:
                    symbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                    break;

                case PropertyDeclarationSyntax propertyDeclaration:
                    var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);

                    // Don't continue with the analysis if it's a full property with accessors
                    // In that case, the accessors are analyzed separately
                    if ((propertySymbol?.GetMethod is not null && propertySymbol?.SetMethod is not null)
                        || (propertySymbol?.GetMethod is null && propertySymbol?.SetMethod is not null))
                    {
                        return false;
                    }

                    var propertySyntaxRef = propertySymbol?.DeclaringSyntaxReferences.FirstOrDefault();
                    if (propertySyntaxRef is not null && propertySyntaxRef.GetSyntax() is PropertyDeclarationSyntax basePropertyDeclaration)
                    {
                        if (basePropertyDeclaration.ExpressionBody is null)
                        {
                            return false;
                        }
                    }

                    symbol = propertySymbol;
                    break;

                case AccessorDeclarationSyntax accessorDeclaration:
                    symbol = semanticModel.GetDeclaredSymbol(accessorDeclaration);
                    break;

                case LocalFunctionStatementSyntax localFunction:
                    symbol = semanticModel.GetDeclaredSymbol(localFunction);
                    break;

                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    symbol = semanticModel.GetSymbolInfo(anonymousFunction).Symbol as IMethodSymbol;
                    break;

                default:
                    // Continue up to next node
                    continue;
            }

            if (symbol is not null)
            {
                if (IsExceptionDeclaredInSymbol(symbol, exceptionType))
                    return true;

                if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                {
                    // Break because you are analyzing a local function or anonymous function (lambda)
                    // If you don't then it will got to the method, and it will affect analysis of this inline function.
                    break;
                }
            }
        }

        return false;
    }

    private static bool IsExceptionDeclaredInSymbol(ISymbol symbol, INamedTypeSymbol exceptionType)
    {
        if (symbol is null)
            return false;

        var declaredExceptionTypes = GetExceptionTypes(symbol);

        foreach (var declaredExceptionType in declaredExceptionTypes)
        {
            if (exceptionType.Equals(declaredExceptionType, SymbolEqualityComparer.Default))
                return true;

            // Check if the declared exception is a base type of the thrown exception
            if (exceptionType.InheritsFrom(declaredExceptionType))
                return true;
        }

        return false;
    }

    private static List<INamedTypeSymbol> GetExceptionTypes(ISymbol symbol)
    {
        // Get exceptions from Throws attributes
        var exceptionAttributes = GetThrowsAttributes(symbol);

        return GetDistictExceptionTypes(exceptionAttributes).ToList();
    }
}