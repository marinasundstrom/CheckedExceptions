using System.Collections.Immutable;
using System.Net.NetworkInformation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{

    private void CheckForRedundantThrowsHandledByDeclaredSuperClass(
        SymbolAnalysisContext context,
        ImmutableArray<AttributeData> throwsAttributes)
    {
        var declaredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var typeToAttributeMap = new Dictionary<INamedTypeSymbol, AttributeData>(SymbolEqualityComparer.Default);

        foreach (var attrData in throwsAttributes)
        {
            var exceptionTypes = GetExceptionTypes(attrData);
            foreach (var exceptionType in exceptionTypes)
            {
                if (exceptionType == null)
                    continue;

                declaredTypes.Add(exceptionType);
                typeToAttributeMap[exceptionType] = attrData;
            }
        }

        foreach (var type in declaredTypes)
        {
            foreach (var otherType in declaredTypes)
            {
                if (type.Equals(otherType, SymbolEqualityComparer.Default))
                    continue;

                if (IsSubclassOf(type, otherType))
                {
                    if (typeToAttributeMap.TryGetValue(type, out var attr))
                    {
                        var syntaxRef = attr.ApplicationSyntaxReference;
                        if (syntaxRef?.GetSyntax(context.CancellationToken) is AttributeSyntax syntax)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DuplicateThrowsByHierarchyDiagnostic,
                                syntax.GetLocation(),
                                otherType.ToDisplayString()));
                        }
                    }
                    break;
                }
            }
        }

        static bool IsSubclassOf(INamedTypeSymbol derived, INamedTypeSymbol baseType)
        {
            var current = derived.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }

    private void CheckForRedundantThrowsHandledByDeclaredSuperClass(
        IEnumerable<AttributeSyntax> throwsAttributes,
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;

        // Store all declared exception types
        var declaredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var typeToSyntaxMap = new Dictionary<INamedTypeSymbol, AttributeSyntax>(SymbolEqualityComparer.Default);

        // Collect all exception types from all [Throws] attributes
        foreach (var throwsAttribute in throwsAttributes)
        {
            var exceptionTypes = GetExceptionTypes(throwsAttribute, semanticModel);
            foreach (var exceptionType in exceptionTypes)
            {
                // Skip unresolved or non-exception types
                if (exceptionType == null)
                    continue;

                declaredTypes.Add(exceptionType);
                typeToSyntaxMap[exceptionType] = throwsAttribute;
            }
        }

        // Check for covered types
        foreach (var type in declaredTypes)
        {
            foreach (var otherType in declaredTypes)
            {
                if (type.Equals(otherType, SymbolEqualityComparer.Default))
                    continue;

                if (IsSubclassOf(type, otherType))
                {
                    // type is redundant, covered by otherType
                    var syntax = typeToSyntaxMap[type];
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateThrowsByHierarchyDiagnostic,
                        syntax.GetLocation(),
                        //type.ToDisplayString(),
                        otherType.ToDisplayString()
                    ));
                    break;
                }
            }
        }

        static bool IsSubclassOf(INamedTypeSymbol derived, INamedTypeSymbol baseType)
        {
            var current = derived.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }
}