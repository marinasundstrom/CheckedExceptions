
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void CollectLinqExceptions(
        InvocationExpressionSyntax invocationSyntax,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        AnalyzerSettings settings,
        CancellationToken ct = default)
    {
        if (semanticModel.GetOperation(invocationSyntax, ct) is not IInvocationOperation termOp)
            return;
        if (!IsLinqExtension(termOp.TargetMethod))
            return;

        var name = termOp.TargetMethod.Name;
        var isTerminal = LinqKnowledge.TerminalOps.Contains(name);

        if (!isTerminal)
        {
            // Deferred invocation inside an argument/return is handled at the boundary.
            if (invocationSyntax.FirstAncestorOrSelf<ReturnStatementSyntax>() is not null)
                return;

            if (invocationSyntax.FirstAncestorOrSelf<ArgumentSyntax>() is not null)
                return;

            // If this is neither a terminal operator nor crosses a boundary, ignore
            if (!IsBoundary(termOp))
                return;
        }

        if (isTerminal)
        {
            // harvest predicate/selector on this terminal op
            CollectThrowsFromFunctionalArguments(termOp, exceptionTypes, compilation, semanticModel, settings, ct);

            // add built-ins for the terminal
            if (LinqKnowledge.BuiltIns.TryGetValue(name, out var builtInFactory))
                foreach (var t in builtInFactory(semanticModel.Compilation, termOp.TargetMethod))
                    if (t is not null) exceptionTypes.Add(t);

            // Backtrack upstream
            var source = GetLinqSourceOperation(termOp);
            if (source is null) return;

            CollectDeferredChainExceptions(source, exceptionTypes, semanticModel.Compilation, semanticModel, settings);
        }
        else
        {
            // Deferred query passed across a boundary – collect upstream exceptions
            CollectDeferredChainExceptions_ForEnumeration(termOp, exceptionTypes, compilation, semanticModel, settings, ct);
        }
    }

    // --- helpers ---

    private static bool IsBoundary(IOperation op)
    {
        for (var parent = op.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case IArgumentOperation:
                case IReturnOperation:
                    return true;
                case IConversionOperation or IParenthesizedOperation:
                    continue;
                default:
                    return false;
            }
        }
        return false;
    }

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
        if (op.TargetMethod.IsExtensionMethod)
            return op.Instance ?? (op.Arguments.Length > 0 ? op.Arguments[0].Value : null);
        return op.Instance;
    }

    // change signature so we can use the semantic model inside
    private static void CollectDeferredChainExceptions(
        IOperation source,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        AnalyzerSettings settings)
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
                        // 1) harvest lambdas/method groups on deferred op
                        CollectThrowsFromFunctionalArguments(inv, exceptionTypes, compilation, semanticModel, settings, default);

                        // 2) add intrinsic deferred-op exceptions (e.g., Cast<T>)
                        if (LinqKnowledge.DeferredBuiltIns.TryGetValue(name, out var defFactory))
                            foreach (var t in defFactory(compilation, inv))
                                if (t is not null) exceptionTypes.Add(t);

                        current = GetLinqSourceOperation(inv);
                        continue;
                    }

                    if (LinqKnowledge.TerminalOps.Contains(name))
                    {
                        // NEW: harvest lambdas/method groups on terminal op too
                        CollectThrowsFromFunctionalArguments(inv, exceptionTypes, compilation, semanticModel, settings, default);

                        if (LinqKnowledge.BuiltIns.TryGetValue(name, out var builtInFactory))
                            foreach (var t in builtInFactory(compilation, inv.TargetMethod))
                                if (t is not null) exceptionTypes.Add(t);

                        current = GetLinqSourceOperation(inv);
                        continue;
                    }

                    // Unknown op: still inspect functional args
                    CollectThrowsFromFunctionalArguments(inv, exceptionTypes, compilation, semanticModel, settings, default);
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

    private void CollectEnumerationExceptions(
        IOperation collection,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        AnalyzerSettings settings,
        CancellationToken ct)
    {
        // If the collection is materialized (e.g., via ToArray()), the terminal invocation
        // itself will surface any exceptions. In that case, diagnostics are handled by
        // the invocation analysis, and we skip boundary reporting.
        // Peel conversions/parentheses to check for terminal LINQ materializers
        var inner = collection;
        while (true)
        {
            switch (inner)
            {
                case IConversionOperation conv:
                    inner = conv.Operand; continue;
                case IParenthesizedOperation paren:
                    inner = paren.Operand; continue;
                default:
                    break;
            }
            break;
        }

        if (inner is IInvocationOperation inv &&
            IsLinqExtension(inv.TargetMethod) &&
            LinqKnowledge.TerminalOps.Contains(inv.TargetMethod.Name))
        {
            return;
        }

        // Walk upstream through the LINQ chain, harvesting [Throws] on deferred operators.
        CollectDeferredChainExceptions_ForEnumeration(collection, exceptionTypes, semanticModel.Compilation, semanticModel, settings, ct);
    }

    private static void CollectDeferredChainExceptions_ForEnumeration(
        IOperation source,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        AnalyzerSettings settings,
        CancellationToken ct)
    {
        var current = source;

        while (current is not null)
        {
            switch (current)
            {
                case IInvocationOperation inv when IsLinqExtension(inv.TargetMethod):
                    {
                        var name = inv.TargetMethod.Name;

                        // On enumeration, we care about exceptions contributed by *deferred* ops.
                        if (LinqKnowledge.DeferredOps.Contains(name) || !LinqKnowledge.TerminalOps.Contains(name))
                        {
                            CollectThrowsFromFunctionalArguments(inv, exceptionTypes, compilation, semanticModel, settings, ct);

                            if (LinqKnowledge.DeferredBuiltIns.TryGetValue(name, out var defFactory))
                                foreach (var t in defFactory(compilation, inv))
                                    if (t is not null) exceptionTypes.Add(t);

                            current = GetLinqSourceOperation(inv);
                            continue;
                        }

                        // If someone put a terminal op directly in foreach (rare), add its built-ins too.
                        if (LinqKnowledge.TerminalOps.Contains(name))
                        {
                            if (LinqKnowledge.BuiltIns.TryGetValue(name, out var builtInFactory))
                                foreach (var t in builtInFactory(compilation, inv.TargetMethod))
                                    if (t is not null) exceptionTypes.Add(t);

                            current = GetLinqSourceOperation(inv);
                            continue;
                        }

                        // Unknown op: still inspect functional args and continue upstream.
                        CollectThrowsFromFunctionalArguments(inv, exceptionTypes, compilation, semanticModel, settings, ct);
                        current = GetLinqSourceOperation(inv);
                        continue;
                    }

                case ILocalReferenceOperation lref:
                    {
                        // Hop into "var query = <initializer>;"
                        var local = lref.Local;
                        foreach (var sr in local.DeclaringSyntaxReferences)
                        {
                            var node = sr.GetSyntax(ct);
                            if (node is VariableDeclaratorSyntax v && v.Initializer?.Value is { } initExpr)
                            {
                                var initOp = semanticModel.GetOperation(initExpr, ct);
                                if (initOp is not null) { current = initOp; goto ContinueWhile; }
                            }
                        }
                        return;
                    }

                case IConversionOperation conv: current = conv.Operand; continue;
                case IParenthesizedOperation paren: current = paren.Operand; continue;
                case IConditionalAccessOperation cond: current = cond.Operation; continue;

                // Optional: handle field/property initializers similarly to locals if you want deeper coverage

                default:
                    return;
            }

        ContinueWhile:;
        }
    }

    private static void CollectThrowsFromFunctionalArguments(
      IInvocationOperation op,
      HashSet<INamedTypeSymbol> exceptionTypes,
      Compilation compilation,
      SemanticModel? semanticModel,
      AnalyzerSettings settings,
      CancellationToken ct = default)
    {
        foreach (var arg in op.Arguments)
        {
            switch (arg.Value)
            {
                case IAnonymousFunctionOperation lambda:
                    CollectThrowsFromSymbol(lambda.Symbol, exceptionTypes, compilation, semanticModel, settings);
                    break;

                case IDelegateCreationOperation del:
                    if (del.Target is IAnonymousFunctionOperation anon)
                        CollectThrowsFromSymbol(anon.Symbol, exceptionTypes, compilation, semanticModel, settings);
                    else if (del.Target is IMethodReferenceOperation mref1)
                        CollectThrowsFromSymbol(mref1.Method, exceptionTypes, compilation, semanticModel, settings, mref1.Syntax);
                    break;

                case IMethodReferenceOperation mref2:
                    CollectThrowsFromSymbol(mref2.Method, exceptionTypes, compilation, semanticModel, settings, mref2.Syntax);
                    break;

                case ILocalReferenceOperation lref when semanticModel is not null:
                    FollowDelegateLocal(lref, exceptionTypes, compilation, semanticModel, settings, ct);
                    break;

                case IParameterReferenceOperation pref when semanticModel is not null:
                    FollowDelegateParameter(pref, exceptionTypes, compilation, semanticModel, ct);
                    break;
            }
        }
    }

    private static void FollowDelegateLocal(
        ILocalReferenceOperation lref,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        AnalyzerSettings settings,
        CancellationToken ct,
        HashSet<ISymbol>? visited = null)
    {
        visited ??= new(SymbolEqualityComparer.Default);
        if (!visited.Add(lref.Local))
            return;

        foreach (var sr in lref.Local.DeclaringSyntaxReferences)
        {
            var node = sr.GetSyntax(ct);
            if (node is VariableDeclaratorSyntax v && v.Initializer?.Value is { } initExpr)
            {
                var initOp = semanticModel.GetOperation(initExpr, ct);
                if (initOp is null) continue;

                if (initOp is IAnonymousFunctionOperation lambda)
                    CollectThrowsFromSymbol(lambda.Symbol, exceptionTypes, compilation, semanticModel, settings);
                else if (initOp is IMethodReferenceOperation mref)// the method group is used at 'initExpr'
                    CollectThrowsFromSymbol(mref.Method, exceptionTypes, compilation, semanticModel, settings, initExpr);
                else if (initOp is IDelegateCreationOperation del)
                {
                    if (del.Target is IAnonymousFunctionOperation anon)
                        CollectThrowsFromSymbol(anon.Symbol, exceptionTypes, compilation, semanticModel, settings);
                    else if (del.Target is IMethodReferenceOperation mref2)
                        CollectThrowsFromSymbol(mref2.Method, exceptionTypes, compilation, semanticModel, settings, initExpr);
                }
                else if (initOp is ILocalReferenceOperation innerLocal)
                    FollowDelegateLocal(innerLocal, exceptionTypes, compilation, semanticModel, settings, ct, visited);
            }
        }
    }

    private static void FollowDelegateParameter(
        IParameterReferenceOperation pref,
        HashSet<INamedTypeSymbol> exceptionTypes,
        Compilation compilation,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        // If you want, you can look up method invocations in the current scope and see 
        // what is being passed to this parameter. That’s more like interprocedural.
        // For now: skip or use AdditionalFiles metadata for known parameters.
    }

    /// <param name="usageSiteNode">Only passed when symbol is a method group</param>
    private static void CollectThrowsFromSymbol(
     ISymbol? symbol,
     HashSet<INamedTypeSymbol> exceptionTypes,
     Compilation compilation,
     SemanticModel semanticModel,
     AnalyzerSettings settings,
     SyntaxNode? usageSiteNode = null,
     CancellationToken ct = default)
    {
        if (symbol is null) return;

        if (symbol is IMethodSymbol methodSymbol)
        {
            // 1) Source bodies (method group to user code / local func)
            if (methodSymbol.DeclaringSyntaxReferences.Length > 0)
            {
                foreach (var sr in methodSymbol.DeclaringSyntaxReferences)
                {
                    var syntax = sr.GetSyntax(ct);
                    SyntaxNode? body = syntax switch
                    {
                        MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody?.Expression,
                        LocalFunctionStatementSyntax lf => (SyntaxNode?)lf.Body ?? lf.ExpressionBody?.Expression,
                        AnonymousFunctionExpressionSyntax af => (SyntaxNode?)af.Block ?? af.ExpressionBody,
                        _ => null
                    };

                    if (body is not null)
                    {
                        // Reuse your existing collectors
                        if (body is BlockSyntax b)
                            exceptionTypes.AddRange(CollectExceptionsFromStatement(b, compilation, semanticModel, settings));
                        else
                            exceptionTypes.AddRange(CollectExceptionsFromExpression(body, compilation, semanticModel, settings));
                    }
                }
            }
            else
            {
                // 2) Metadata/BCL: use a tiny knowledge base (fast path for int.Parse, etc.)
                foreach (var t in GetKnownMethodExceptions(methodSymbol, compilation))
                    exceptionTypes.Add(t);
            }

            var isAnonymousFunctionOrLambda = methodSymbol.MethodKind == MethodKind.AnonymousFunction;

            // 3) Optional XML interop (your existing logic)
            if (!isAnonymousFunctionOrLambda && settings.IsXmlInteropEnabled)
            {
                var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);

                if (usageSiteNode is not null)
                {
                    xmlExceptionTypes = ProcessNullable(compilation, semanticModel, usageSiteNode, methodSymbol, xmlExceptionTypes);
                }

                if (xmlExceptionTypes.Any())
                    exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
            }
        }

        // 4) Attributes
        foreach (var attr in symbol.GetAttributes())
            foreach (var t in GetExceptionTypesFromThrowsAttribute(attr))
                exceptionTypes.Add(t);
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
            "Reverse", "Zip", "DefaultIfEmpty",
            "Cast", "OfType", "AsEnumerable"
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
    public static ImmutableDictionary<string, Func<Compilation, IMethodSymbol, IEnumerable<INamedTypeSymbol>>> BuiltIns
        = new Dictionary<string, Func<Compilation, IMethodSymbol, IEnumerable<INamedTypeSymbol>>>(StringComparer.Ordinal)
        {
            // Empty-sequence semantics
            ["First"] = (c, m) => Types(c, "System.InvalidOperationException"),
            ["Last"] = (c, m) => Types(c, "System.InvalidOperationException"),
            ["Single"] = (c, m) => Types(c, "System.InvalidOperationException"),
            ["FirstOrDefault"] = (c, m) => Array.Empty<INamedTypeSymbol>(),                   // no throw on empty
            ["LastOrDefault"] = (c, m) => Array.Empty<INamedTypeSymbol>(),                   // no throw on empty
            ["SingleOrDefault"] = (c, m) => Types(c, "System.InvalidOperationException"),      // >1 element

            // Indexing
            ["ElementAt"] = (c, m) => Types(c, "System.ArgumentOutOfRangeException"),
            ["ElementAtOrDefault"] = (c, m) => Array.Empty<INamedTypeSymbol>(),

            // Aggregates: empty sequence behavior depends on overloads / nullability
            ["Min"] = (c, m) => ReturnsNonNullableValue(m) ? Types(c, "System.InvalidOperationException") : [],
            ["Max"] = (c, m) => ReturnsNonNullableValue(m) ? Types(c, "System.InvalidOperationException") : [],
            ["Average"] = (c, m) => ReturnsNonNullableValue(m) ? Types(c, "System.InvalidOperationException") : Enumerable.Empty<INamedTypeSymbol>()
                                                           // + decimal overflow below
                                                           .Concat(IsDecimalAverage(m) ? Types(c, "System.OverflowException") : []),
            // Sum: only decimal variants can overflow (decimal arithmetic is checked)
            ["Sum"] = (c, m) => IsDecimalSum(m) ? Types(c, "System.OverflowException") : [],

            // Dictionary materialization during enumeration (duplicate keys)
            ["ToDictionary"] = (c, m) => Types(c, "System.ArgumentException"),

            // Lookup allows duplicate keys → no built-in throw on enumeration
            ["ToLookup"] = (c, m) => Array.Empty<INamedTypeSymbol>(),

            // Aggregate: without seed throws on empty; with seed doesn’t
            ["Aggregate"] = (c, m) => AggregateThrowsOnEmpty(m) ? Types(c, "System.InvalidOperationException") : [],

            // The rest are generally fine on empty / no built-ins at enumeration time:
            ["Any"] = (c, m) => [],
            ["All"] = (c, m) => [],
            ["Contains"] = (c, m) => [],
            ["Count"] = (c, m) => [],
            ["LongCount"] = (c, m) => [],
            ["SequenceEqual"] = (c, m) => [],
            ["ToArray"] = (c, m) => [],
            ["ToList"] = (c, m) => [],
        }.ToImmutableDictionary();

    public static ImmutableDictionary<string, Func<Compilation, IInvocationOperation, IEnumerable<INamedTypeSymbol>>> DeferredBuiltIns
            = new Dictionary<string, Func<Compilation, IInvocationOperation, IEnumerable<INamedTypeSymbol>>>(StringComparer.Ordinal)
            {
                // Cast<T>() will throw InvalidCastException during enumeration if an element can't be cast
                ["Cast"] = (comp, inv) =>
                {
                    // T in Cast<T>()
                    if (inv.TargetMethod.TypeArguments.Length is not 1)
                        return Array.Empty<INamedTypeSymbol>();
                    var targetT = inv.TargetMethod.TypeArguments[0];

                    var semanticModel = comp.GetSemanticModel(inv.Syntax.SyntaxTree);

                    // try to recover S
                    var srcElem = ResolveSourceElementType(inv, /* you have it in the caller */ semanticModel, default);

                    // unknown => be pessimistic (may throw during enumeration)
                    if (srcElem is null)
                        return Types(comp, "System.InvalidCastException");

                    var conv = comp.ClassifyCommonConversion(srcElem, targetT);

                    // Safe cases: identity or any implicit conversion (ref upcast, boxing, etc.)
                    return (conv.IsIdentity || conv.IsImplicit)
                        ? Array.Empty<INamedTypeSymbol>()
                        : Types(comp, "System.InvalidCastException");
                }

                // You can add others later if you decide to model them (most deferred ops don't throw intrinsically)
            }.ToImmutableDictionary();

    private static ITypeSymbol? ResolveSourceElementType(
        IInvocationOperation castInvocation,   // the Cast<T>() invocation
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        // 1) where does Cast<T> read from? (receiver for reduced, arg[0] otherwise)
        var srcOp = castInvocation.Instance ?? (castInvocation.Arguments.Length > 0
            ? castInvocation.Arguments[0].Value
            : null);

        return GetElemFromOp(srcOp);

        ITypeSymbol? GetElemFromOp(IOperation? op)
        {
            if (op is null) return null;

            // a) If the op's type already exposes IEnumerable<T>, use it
            var elem = GetEnumerableElementType(op.Type);
            if (elem is not null) return elem;

            // b) Peel implicit conversions like "(IEnumerable) xs"
            if (op is IConversionOperation conv)
                return GetElemFromOp(conv.Operand);

            // c) Follow locals to their initializer
            if (op is ILocalReferenceOperation lref)
            {
                foreach (var sr in lref.Local.DeclaringSyntaxReferences)
                {
                    var node = sr.GetSyntax(ct);
                    if (node is VariableDeclaratorSyntax v && v.Initializer?.Value is { } initExpr)
                    {
                        var initOp = semanticModel.GetOperation(initExpr, ct);
                        var fromInit = GetElemFromOp(initOp);
                        if (fromInit is not null) return fromInit;
                    }
                }
            }

            // d) If it’s another invocation (Where/Select/etc.), its Type is usually IEnumerable<T>
            if (op is IInvocationOperation inv)
            {
                var fromInv = GetEnumerableElementType(inv.Type);
                if (fromInv is not null) return fromInv;

                // also try its receiver
                var fromRecv = GetElemFromOp(inv.Instance);
                if (fromRecv is not null) return fromRecv;
            }

            // e) Parenthesized/conditional access wrappers
            if (op is IParenthesizedOperation paren) return GetElemFromOp(paren.Operand);
            if (op is IConditionalAccessOperation cond) return GetElemFromOp(cond.Operation);

            // f) we’re out of luck
            return null;
        }
    }

    private static ITypeSymbol? GetEnumerableElementType(ITypeSymbol? t)
    {
        if (t is null) return null;
        if (t is IArrayTypeSymbol arr) return arr.ElementType;

        IEnumerable<INamedTypeSymbol> ifaces = t.AllInterfaces;
        if (t is INamedTypeSymbol self) ifaces = ifaces.Prepend(self);

        foreach (var i in ifaces)
            if (i.IsGenericType &&
                i.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                return i.TypeArguments[0];

        return null;
    }

    private static IEnumerable<INamedTypeSymbol> Types(Compilation c, params string[] metadata)
        => metadata.Select(n => c.GetTypeByMetadataName(n)).Where(t => t is not null)!;

    private static bool ReturnsNonNullableValue(IMethodSymbol m)
    {
        // Min/Max/Average over non-nullable value types throw on empty
        var rt = m.ReturnType;
        return rt.IsValueType && !IsNullable(rt);
    }

    private static bool IsNullable(ITypeSymbol t)
        => t.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool IsDecimal(ITypeSymbol t)
        => t.SpecialType == SpecialType.System_Decimal;

    // Average has overloads returning decimal/decimal?, double/double?, float/float?
    // We only care to add OverflowException for decimal returning variants.
    private static bool IsDecimalAverage(IMethodSymbol m)
        => IsDecimal(m.ReturnType);

    // Sum has many overloads; decimal variants can throw OverflowException.
    private static bool IsDecimalSum(IMethodSymbol m)
    {
        // Sum(IEnumerable<decimal>) returns decimal
        // Sum(IEnumerable<decimal?>) returns decimal?
        if (m.Parameters.Length is 1)
            return IsEnumerableOf(m.Parameters[0].Type, IsDecimal);

        // Sum<TSource>(IEnumerable<TSource>, Func<TSource, decimal>) etc.
        if (m.Parameters.Length >= 2)
            return ReturnsDecimalSelector(m.Parameters[1].Type);
        return false;
    }

    private static bool IsEnumerableOf(ITypeSymbol type, Func<ITypeSymbol, bool> isT)
    {
        if (type is INamedTypeSymbol nt && nt.IsGenericType)
        {
            var def = nt.ConstructedFrom;
            if (def.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
                return isT(nt.TypeArguments[0]) || (IsNullable(nt.TypeArguments[0]) && isT(((INamedTypeSymbol)nt.TypeArguments[0]).TypeArguments[0]));
        }
        return false;
    }

    private static bool ReturnsDecimalSelector(ITypeSymbol selectorType)
    {
        // Func<TSource, decimal> or Func<TSource, decimal?>
        if (selectorType is INamedTypeSymbol f && f.DelegateInvokeMethod is { } invoke)
            return IsDecimal(invoke.ReturnType) || (IsNullable(invoke.ReturnType) && IsDecimal(((INamedTypeSymbol)invoke.ReturnType).TypeArguments[0]));
        return false;
    }

    private static bool AggregateThrowsOnEmpty(IMethodSymbol m)
    {
        // Aggregate<TSource>(IEnumerable<TSource>, Func<TSource,TSource,TSource>)  // no seed → throws on empty
        // Any overload where the first parameter after the source is NOT a seed implies "no seed"
        // Seed overloads: Aggregate<TSource,TAccumulate>(IEnumerable<TSource>, TAccumulate, Func<...>, ...)
        if (!m.IsExtensionMethod || m.Parameters.Length < 2) return false;

        var second = m.Parameters[1];
        // Heuristic: if second parameter is a delegate (Func<...>) => no seed overload, throws on empty.
        return second.Type.TypeKind == TypeKind.Delegate;
    }
}