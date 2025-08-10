# LINQ support

The analyzer has initial support for LINQ through the standard query operators.

More will be added in future releases.

## Query methods

```csharp
IEnumerable<int> items = [1, 42, 3];
var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));

// On First():
// THROW001: Unhandled exception type 'FormatException'
// THROW001: Unhandled exception type 'OverflowException'
// THROW001: Unhandled exception type 'InvalidOperationException' // From First()
var r = query.First();
```

## Enumerating with `foreach`

It even works for `foreach`, with diagnostics being issued.

```csharp
IEnumerable<int> items = [1, 42, 3];
var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));

// Reported on "query":
// THROW001: Unhandled exception type 'FormatException'
// THROW001: Unhandled exception type 'OverflowException'
foreach (var item in query)
{

}
```