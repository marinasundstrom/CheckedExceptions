using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    /// <summary>
    /// Analyzes exceptions thrown by a property, specifically its getters and setters.
    /// </summary>
    private void AnalyzePropertyExceptions(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, IPropertySymbol propertySymbol,
        AnalyzerSettings settings)
    {
        HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(context, expression, propertySymbol, settings);

        // Deduplicate and analyze each distinct exception type
        foreach (var exceptionType in exceptionTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            AnalyzeExceptionThrowingNode(context, expression, exceptionType, settings);
        }
    }

    private static HashSet<INamedTypeSymbol> GetPropertyExceptionTypes(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, IPropertySymbol propertySymbol, AnalyzerSettings settings)
    {
        // Determine if the analyzed expression is for a getter or setter
        bool isGetter = IsPropertyGetter(expression);
        bool isSetter = IsPropertySetter(expression);

        // List to collect all relevant exception types
        var exceptionTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        IEnumerable<ExceptionInfo> getterExceptions = [];
        IEnumerable<ExceptionInfo> setterExceptions = [];
        IEnumerable<ExceptionInfo> allOtherExceptions = [];

        if (settings.IsXmlInteropEnabled)
        {
            // Retrieve exception types documented in XML comments for the property
            var xmlDocumentedExceptions = GetExceptionTypesFromDocumentationCommentXml(context.Compilation, propertySymbol).ToList();

            // Filter exceptions documented specifically for the getter and setter
            getterExceptions = xmlDocumentedExceptions.Where(x => HeuristicRules.IsForGetter(x.Description));

            setterExceptions = xmlDocumentedExceptions.Where(x => HeuristicRules.IsForSetter(x.Description));

            if (isSetter && propertySymbol.SetMethod is not null)
            {
                // Will filter away 
                setterExceptions = ProcessNullable(context, expression, propertySymbol.SetMethod, setterExceptions);
            }

            // Handle exceptions that don't explicitly belong to getters or setters
            allOtherExceptions = xmlDocumentedExceptions
                .Except(getterExceptions);
            allOtherExceptions = allOtherExceptions
                .Except(setterExceptions);

            if (isSetter && propertySymbol.SetMethod is not null)
            {
                allOtherExceptions = ProcessNullable(context, expression, propertySymbol.SetMethod, allOtherExceptions);
            }
        }

        // Analyze exceptions thrown by the getter if applicable
        if (isGetter && propertySymbol.GetMethod is not null)
        {
            var getterMethodExceptions = GetExceptionTypes(propertySymbol.GetMethod);
            exceptionTypes.AddRange(getterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(getterMethodExceptions);
        }

        // Analyze exceptions thrown by the setter if applicable
        if (isSetter && propertySymbol.SetMethod is not null)
        {
            var setterMethodExceptions = GetExceptionTypes(propertySymbol.SetMethod);
            exceptionTypes.AddRange(setterExceptions.Select(x => x.ExceptionType));
            exceptionTypes.AddRange(setterMethodExceptions);
        }

        if (propertySymbol.GetMethod is not null)
        {
            allOtherExceptions = ProcessNullable(context, expression, propertySymbol.GetMethod, allOtherExceptions);
        }

        // Add other exceptions not specific to getters or setters
        exceptionTypes.AddRange(allOtherExceptions.Select(x => x.ExceptionType));
        return exceptionTypes;
    }

    private static bool IsPropertyGetter(ExpressionSyntax expression)
    {
        var parent = expression.Parent;

        if (parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.Left == expression)
                return false; // It's a setter
        }
        else if (parent is PrefixUnaryExpressionSyntax prefixUnary)
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression) || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
                return false; // It's a setter
        }
        else if (parent is PostfixUnaryExpressionSyntax postfixUnary)
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression) || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
                return false; // It's a setter
        }

        return true; // Assume getter in other cases
    }

    private static bool IsPropertySetter(ExpressionSyntax expression)
    {
        var parent = expression.Parent;

        if (parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.Left == expression)
                return true; // It's a setter
        }
        else if (parent is PrefixUnaryExpressionSyntax prefixUnary)
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression) || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
                return true; // It's a setter
        }
        else if (parent is PostfixUnaryExpressionSyntax postfixUnary)
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression) || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
                return true; // It's a setter
        }

        return false; // Assume getter in other cases
    }

    private static void CheckNoThrowsOnFullPropertyDecl(SyntaxNodeAnalysisContext context, IEnumerable<AttributeSyntax> throwsAttributes)
    {
        foreach (var throwsAttribute in throwsAttributes)
        {
            // Report invalid throws declaration
            var diagnostic = Diagnostic.Create(
                        RuleThrowsDeclarationNotValidOnFullProperty,
                        throwsAttribute.GetLocation());

            context.ReportDiagnostic(diagnostic);
        }
    }
}