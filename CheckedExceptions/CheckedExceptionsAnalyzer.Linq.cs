
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private void CollectLinqExceptions(
        InvocationExpressionSyntax invocationSyntax,
        HashSet<INamedTypeSymbol> exceptionTypes,
        SemanticModel semanticModel,
        CancellationToken ct = default)
    {
        if (semanticModel.GetOperation(invocationSyntax, ct) is not IInvocationOperation termOp)
            return;

        if (!IsLinqExtension(termOp.TargetMethod))
            return;

        if (!LinqKnowledge.TerminalOps.Contains(termOp.TargetMethod.Name))
            return;

        if (LinqKnowledge.BuiltIns.TryGetValue(termOp.TargetMethod.Name, out var builtInFactory))
            foreach (var t in builtInFactory(semanticModel.Compilation)) if (t is not null) exceptionTypes.Add(t);

        var source = GetLinqSourceOperation(termOp);
        if (source is null) return;

        CollectDeferredChainExceptions(source, exceptionTypes, semanticModel.Compilation, semanticModel);
    }

    // --- helpers ---

    private static bool IsLinqExtension(IMethodSymbol method)
    {
        if (method is null || !method.IsExtensionMethod) return false;

        var containingType = method.ContainingType;
        if (containingType is null) return false;

        // System.Linq.Enumerable or System.Linq.Queryable
        return containingType.Name is "Enumerable" or "Queryable"
            && containingType.ContainingNamespace?.ToDisplayString() == "System.Linq";
    }

    private static IOperation? GetLinqSourceOperation(IInvocationOperation op)
    {
        // For extension methods, Instance is null; the source is the 1st argument.
        // For instance methods (rare in LINQ), Instance is the source.
        if (op.TargetMethod.IsExtensionMethod)
            return op.Arguments.Length > 0 ? op.Arguments[0].Value : null;

        return op.Instance;
    }

    // change signature so we can use the semantic model inside
    private static void CollectDeferredChainExceptions(
        IOperation source,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel)
    {
        var current = source;

        while (current is not null)
        {
            switch (current)
            {
                case IInvocationOperation inv when IsLinqExtension(inv.TargetMethod):
                    var name = inv.TargetMethod.Name;

                    if (LinqKnowledge.DeferredOps.Contains(name))
                    {
                        CollectThrowsFromLambdaArguments(inv, exceptionTypes);
                        current = GetLinqSourceOperation(inv);
                        continue;
                    }

                    if (LinqKnowledge.TerminalOps.Contains(name))
                    {
                        if (LinqKnowledge.BuiltIns.TryGetValue(name, out var builtInFactory))
                            foreach (var t in builtInFactory(compilation)) if (t is not null) exceptionTypes.Add(t);

                        current = GetLinqSourceOperation(inv);
                        continue;
                    }

                    // Unknown LINQ op: still harvest lambdas
                    CollectThrowsFromLambdaArguments(inv, exceptionTypes);
                    current = GetLinqSourceOperation(inv);
                    continue;

                case ILocalReferenceOperation lref:
                    // hop into "var query = <initializer>;"
                    var local = lref.Local;
                    foreach (var sr in local.DeclaringSyntaxReferences)
                    {
                        var node = sr.GetSyntax();
                        if (node is VariableDeclaratorSyntax v && v.Initializer?.Value is { } initExpr)
                        {
                            var initOp = semanticModel.GetOperation(initExpr);
                            if (initOp is not null) { current = initOp; goto continueWhile; }
                        }
                    }
                    return;

                case IConversionOperation conv:
                    current = conv.Operand; continue;

                case IParenthesizedOperation paren:
                    current = paren.Operand; continue;

                // (Optional) Light support for field/property initializers in same file
                // case IFieldReferenceOperation or IPropertyReferenceOperation: try to find initializer similarly.

                default:
                    return;
            }

        continueWhile:;
        }
    }

    private static void CollectThrowsFromLambdaArguments(
      IInvocationOperation op,
      HashSet<INamedTypeSymbol> exceptionTypes)
    {
        foreach (var arg in op.Arguments)
        {
            var lambda = ExtractAnonymousFunction(arg.Value);
            if (lambda?.Symbol is not { } sym) continue;

            foreach (var attr in sym.GetAttributes())
            {
                foreach (var t in GetExceptionTypesFromThrowsAttribute(attr))
                {
                    exceptionTypes.Add(t);
                }
            }
        }
    }

    // Return 0..N types from [Throws(typeof(...), typeof(...))]
    private static IEnumerable<INamedTypeSymbol> GetExceptionTypesFromThrowsAttribute(AttributeData attr)
    {
        if (attr.AttributeClass?.Name != "ThrowsAttribute")
            yield break;

        // Handle ctor arg as array or params
        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol single)
            {
                yield return single;
            }
            else if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var el in arg.Values)
                    if (el.Kind == TypedConstantKind.Type && el.Value is INamedTypeSymbol t)
                        yield return t;
            }
        }
    }

    private static IAnonymousFunctionOperation? ExtractAnonymousFunction(IOperation value)
    {
        return value switch
        {
            IAnonymousFunctionOperation f => f,
            IDelegateCreationOperation d when d.Target is IAnonymousFunctionOperation f => f,
            _ => null
        };
    }

    private static bool IsThrowsAttribute(AttributeData attr, out INamedTypeSymbol? exceptionType)
    {
        exceptionType = null;

        var attrClass = attr.AttributeClass;
        if (attrClass is null) return false;

        // Your actual attribute full name here:
        // e.g., "Sundstrom.CheckedExceptions.ThrowsAttribute"
        var isThrows = attrClass.Name is "ThrowsAttribute";
        if (!isThrows) return false;

        // Expecting [Throws(typeof(...), typeof(...))]
        // Attribute ctor args are typed as System.Type -> mapped to INamedTypeSymbol via AttributeData
        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg.Kind != TypedConstantKind.Array)
                continue;

            foreach (var el in ctorArg.Values)
            {
                if (el.Kind == TypedConstantKind.Type && el.Value is INamedTypeSymbol t)
                    exceptionType ??= t; // we'll add all below anyway
            }
        }

        // Add all declared types
        var all = attr.ConstructorArguments
            .Where(a => a.Kind == TypedConstantKind.Array)
            .SelectMany(a => a.Values)
            .Where(v => v.Kind == TypedConstantKind.Type && v.Value is INamedTypeSymbol)
            .Select(v => (INamedTypeSymbol)v.Value!)
            .ToArray();

        if (all.Length > 0)
        {
            // The caller adds them; we just indicate it's a Throws attribute.
            // We’ve already surfaced one via exceptionType (for the return bool),
            // but the collector iterates attributes and adds all anyway.
            exceptionType = all[0];
            return true;
        }

        return false;
    }
}

internal static class LinqKnowledge
{
    // Minimal sets — extend as you go.
    public static readonly HashSet<string> DeferredOps = new(StringComparer.Ordinal)
    {
        "Where", "Select", "SelectMany",
        "Take", "Skip", "TakeWhile", "SkipWhile",
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "GroupBy", "Join", "GroupJoin",
        "Distinct", "Concat", "Union", "Intersect", "Except",
        "Reverse", "Zip", "DefaultIfEmpty"
    };

    public static readonly HashSet<string> TerminalOps = new(StringComparer.Ordinal)
    {
        "ToArray", "ToList", "ToDictionary", "ToLookup",
        "First", "FirstOrDefault", "Single", "SingleOrDefault",
        "Last", "LastOrDefault",
        "Any", "All", "Count", "LongCount",
        "Sum", "Min", "Max", "Average",
        "Contains", "SequenceEqual",
        "ElementAt", "ElementAtOrDefault",
        "Aggregate"
    };

    // Built-in exceptions for terminal ops (add more as needed).
    public static ImmutableDictionary<string, Func<Compilation, IEnumerable<INamedTypeSymbol>>> BuiltIns
        = new Dictionary<string, Func<Compilation, IEnumerable<INamedTypeSymbol>>>(StringComparer.Ordinal)
        {
            ["First"] = c => [Get(c, "System.InvalidOperationException")],
            ["Single"] = c => [Get(c, "System.InvalidOperationException")],
            ["ElementAt"] = c => [Get(c, "System.ArgumentOutOfRangeException")],
            ["ToDictionary"] = c => [Get(c, "System.ArgumentException")], // duplicate keys
            // Note: FirstOrDefault/SingleOrDefault/ElementAtOrDefault generally don't throw "empty" exceptions.
        }.ToImmutableDictionary();

    private static INamedTypeSymbol? Get(Compilation c, string metadataName)
        => c.GetTypeByMetadataName(metadataName);
}