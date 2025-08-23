# LINQ support

The analyzer understands LINQ query operators on both `IEnumerable<T>` and `IAsyncEnumerable<T>` (via [System.Linq.Async](https://www.nuget.org/packages/System.Linq.Async)). Support for `IQueryable<T>` is enabled by default; disable it with `disableLinqQueryableSupport` if your provider cannot translate the expressions.

Async operator names such as `FirstAsync` or `FirstAwaitWithCancellation` are normalized to their synchronous counterparts so the same exception knowledge applies.

> ⚠️ Translation of `IQueryable<T>` expressions depends on the LINQ provider. Disable support with `disableLinqQueryableSupport` if your provider cannot evaluate the expressions similarly to LINQ-to-Objects.

## Deferred execution

LINQ is built on deferred execution. The operations (with their predicates/selectors) get chained, resulting in a chain of query objects that implement `IEnumerable<T>`. The query is executed only when a **terminator** operator runs (e.g., `First`, `Any`, `ToArray`) or when you iterate it with `foreach`. That’s when results are materialized—and where exceptions can occur.

## Query methods

Here’s an example that uses a **terminator with a predicate**. The predicate can throw, and the diagnostic is reported on the terminator call:

```csharp
IEnumerable<string> values = [ "10", "x", "20" ];

var allEven = values
    .Where(v => v.Length > 0)
    .All(v => int.Parse(v) % 2 is 0);

// Reported on All(...):
// THROW001: Unhandled exception type 'FormatException'
// THROW001: Unhandled exception type 'OverflowException'
```

> Exceptions are inferred and implicit on LINQ methods, so no declarations needed. this behavior can be disabled. 

This differs from `First()`/`Single()` cases by not adding its own “empty/duplicate” errors—`All` only reflects exceptions from the predicate.

### Async query methods

Async pipelines work the same way:

```csharp
IAsyncEnumerable<int> items = default!;
var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
var r = await query.FirstAsync();
// THROW001: FormatException, OverflowException, InvalidOperationException
```

## Enumerating with `foreach`

A different shape: a **mid-stream predicate** on a deferred operator, evaluated during enumeration. Diagnostics are issued on the query expression being iterated.

```csharp
IEnumerable<string> dates = [ "2001-01-01", "oops", "2005-12-31" ];

var recent = dates
    .TakeWhile([Throws(s => DateTime.Parse(s).Year >= 2000);

// Reported on "recent":
// THROW001: Unhandled exception type 'FormatException'
foreach (var d in recent)
{
    // Enumeration triggers evaluation of TakeWhile's predicate
}
```

This shows exceptions surfacing *during* enumeration rather than on a terminal method call.

## Caveats

There is no way to indicate that a parameter, field, or property of type `IEnumerable<T>` might be throwing exceptions.

The recommended approach is to handle exceptions from enumerables locally and expose a **materialized** collection type instead.
