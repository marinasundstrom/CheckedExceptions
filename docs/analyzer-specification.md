# Analyzer Specification

## Overview

The **CheckedExceptionsAnalyzer** operates in two complementary layers:

1. **Exception Handling Analysis (core, always on)**

   * Detects thrown exceptions from `throw` statements, `[Throws]` annotations, and `<exception>` XML docs.
   * Ensures each exception is either **caught** in a surrounding `try/catch` or **declared** with `[Throws]`.
   * Produces diagnostics such as **unhandled exceptions** (`THROW001`) and **bad practices** (e.g. `THROW004` for `throw new Exception()`).

2. **Control Flow Analysis (optional)**

   * Performs lightweight reachability analysis to refine diagnostics.
   * Detects **redundant catch clauses** and **unreachable code** (with IDE gray‑out support).
   * Improves accuracy by reporting only exceptions that are truly reachable in context.
   * This analysis can be **disabled** in configuration if only the basic handling checks are desired.

---

## `Throws` attribute

The `[Throws]` attribute is the contract that tells that a method, property accessor, local function, or lambda expression might throw one or more specified exceptions.

Placing a `Throws` declaration on a declaration will mark the specified exception types as *handled* at the call site.

---

## Unhandled exceptions (Core analysis)

The diagnostic for *Unhandled exception types* is **`THROW001`**.

This occurs when an exception is thrown but is neither caught nor declared with `[Throws]`.

**Case 1: Unhandled thrown expression**

```c#
// THROW001: Unhandled exception type 'InvalidOperationException'
throw new InvalidOperationException();
```

**Case 2: Declared exceptions not handled at call site**

```c#
// THROW001: Unhandled exception type 'InvalidOperationException'
TestMethod();

[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}
```

---

## Handling exceptions (Core analysis)

### Exception declarations (`[Throws]`)

By declaring an exception using `[Throws]`, you indicate to the consumer that the exception may be propagated if not caught locally.

```c#
[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}
```

### Supported members

`[Throws]` may be applied to:

* **Methods (and local functions)**
* **Property accessors**
* **Lambda expressions**

Examples:

**Method**

```c#
[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}

// THROW001: Unhandled exception type 'InvalidOperationException'
TestMethod();
```

**Property**

```c#
public class Test 
{
    public int Prop
    {
        [Throws(typeof(InvalidOperationException))]
        get => throw new InvalidOperationException();
    }
}

var test = new Test();

// THROW001: Unhandled exception type 'InvalidOperationException'
var v = test.Prop;
```

**Lambda**

```c#
Func<int, int, int> add = [Throws(typeof(OverflowException))] (a, b) => a + b;

// THROW001: Unhandled exception type 'OverflowException'
add(int.MaxValue, 1);
```

### Handling in `try`/`catch`

You can handle exceptions at throw sites using `try` and `catch`.

If no handler matches, the exception must be declared with `[Throws]`.

```c#
public void Test() 
{
    try 
    {
        TestMethod();
    }
    catch (InvalidOperationException) 
    {
        // handled
    }
}

[Throws(typeof(InvalidOperationException))]
public void TestMethod() => throw new InvalidOperationException();
```

---

## Redundant or invalid handling (Control flow analysis)

Control flow analysis detects unnecessary or incorrectly placed exception handling and declarations.

The general diagnostic for redundant catch clauses is `THROW015`, reported on the catch keyword.
More specific diagnostics may be reported on the catch declaration itself (the exception type).

### Redundant catch clauses

Detected only with control flow analysis:

* **Typed catch never matched** → **`THROW009`**

The specified exception type is never thrown in the associated try block.

  ```c#
  try { } 
  // THROW015: Redundant catch clause
  // THROW009: Exception type 'InvalidOperationException' is never thrown
  catch (InvalidOperationException) { }
  ```

* **Catch‑all with nothing thrown** → **`THROW013`**

No remaining exceptions are left unhandled, making the catch-all clause unnecessary.

  ```c#
  try { } 
  // THROW015: Redundant catch clause
  // THROW013: No remaining exceptions to be handled
  catch { }
  ```

### Redundant exception declarations

Control flow analysis is also used to determine whether declarations are truly necessary:

* **Declared but never thrown** → **`THROW012`**

  ```c#
  [Throws(typeof(InvalidOperationException))] // THROW012
  public void Foo() { }
  ```

* **Duplicate declarations** → **`THROW005`**

* **Already covered by base type** → **`THROW008`**
  (suppressed when `treatThrowsExceptionAsCatchRest` is enabled and the base type is `System.Exception`)

### Invalid placement (Core analysis)

* **Throws on full property (instead of accessor)** → **`THROW010`**

---

## Bad practices with `Exception` (Core analysis)

* **Throwing `System.Exception` directly** → **`THROW004`**
* **Declaring `[Throws(typeof(Exception))]`** → **`THROW003`**

---

## Inheritance hierarchies (Core analysis)

The analyzer ensures consistency across inheritance and interface implementations:

* **Missing exceptions from base/interface** → **`THROW007`**
* **Declaring incompatible exceptions** → **`THROW006`**

Redundant handling is also reported when more general types make specific ones unnecessary (**`THROW008`**, via control flow analysis).

---

## Interop: XML documentation support (Core analysis)

Exceptions from XML documentation are outwardly treated as *declared* by the consumer. This provides compatibility with the .NET class library and third‑party code.

* **Documented but missing `[Throws]`** → **`THROW011`**

Example:

```c#
/// <exception cref="InvalidOperationException" />
public void Foo() { }

// THROW011: Exception 'InvalidOperationException' is documented but not declared with [Throws]
```

A code fix is available to add `[Throws]` from XML docs.

> XML documentation support does **not** replace `[Throws]`. It is an interop feature. To actually declare exceptions in your own code, you must use `[Throws]`.

**Disabling the feature:**

```json
{
  "disableXmlDocInterop": true
}
```

Disabling removes XML doc interop, including .NET class library coverage.

---

### Property heuristics

When `<exception>` documentation is applied to a **property**, the analyzer uses text‑based heuristics to decide which accessor receives the implied `[Throws]`:

* If the text mentions **“get”**, **“gets”**, **“getting”**, or **“retrieved”** → `[Throws]` is applied to the **getter**.
* If the text mentions **“set”**, **“sets”**, or **“setting”** → `[Throws]` is applied to the **setter**.
* If **no keywords are found**:

  * If the property has only a **getter** or only a **setter** → `[Throws]` applies to that accessor.
  * If the property has both → the analyzer **defaults to the getter**.

#### Examples

**Setter keyword**

```c#
/// <exception cref="InvalidOperationException">
/// Thrown if the value cannot be **set**.
/// </exception>
public int Value { get; set; }

// Analyzer interprets this as:
// [Throws(typeof(InvalidOperationException))]
// set { ... }
```

**Getter keyword**

```c#
/// <exception cref="InvalidOperationException">
/// Thrown if the value cannot be **retrieved**.
/// </exception>
public int Value { get; set; }

// Analyzer interprets this as:
// [Throws(typeof(InvalidOperationException))]
// get { ... }
```

**No keyword, defaults to getter**

```c#
/// <exception cref="InvalidOperationException">
/// Thrown if the property is in an invalid state.
/// </exception>
public int Value { get; set; }

// Analyzer interprets this as:
// [Throws(typeof(InvalidOperationException))]
// get { ... }
```

---

#### Expression‑bodied properties

Expression‑bodied properties (`=>`) are treated as a single accessor:

* If it has only a `get` → `[Throws]` applies to the getter.
* If it has only a `set` (rare, but possible with `init` or `set =>`) → `[Throws]` applies to the setter.

Examples:

```c#
/// <exception cref="InvalidOperationException">
/// Thrown when retrieving the value.
/// </exception>
public int Value => throw new InvalidOperationException();

// Analyzer interprets this as:
// [Throws(typeof(InvalidOperationException))]
// get => ...
```

```c#
/// <exception cref="InvalidOperationException">
/// Thrown when attempting to set.
/// </exception>
public int Value { set => throw new InvalidOperationException(); }

// Analyzer interprets this as:
// [Throws(typeof(InvalidOperationException))]
// set => ...
```

---

## Exception classification (Core analysis)

`CheckedExceptions.settings.json` contains an explicit `exceptions` map that classifies each exception type as `Ignored`, `Informational`, or `Strict`.

- **Ignored** – no diagnostics are produced.
- **Informational** – diagnostics are reported but `[Throws]` is not required.
- **Strict** – exceptions must be caught or declared.

Any exception not listed defaults to **Strict**, so unclassified types will trigger `THROW001` until they are handled or declared.

> **Migration note:** Legacy `ignoredExceptions` and `informationalExceptions` properties are still processed for backward compatibility, but they are deprecated and translated into entries in the `exceptions` map.

---

## Casts and conversions (Core analysis, refined by control flow)

The analyzer inspects explicit and implicit **cast syntax** and accounts for possible exceptions raised by the runtime:

* **`InvalidCastException`** – when a reference conversion might be invalid.
* **`OverflowException`** – when a numeric conversion is checked and might exceed the target type’s range.

Example:

```c#
// THROW001: Unhandled exception type 'InvalidCastException'
object o = "hello";
int i = (int)o; // invalid cast

// THROW001: Unhandled exception type 'OverflowException'
checked
{
    long l = long.MaxValue;
    int i2 = (int)l; // overflow

    // Truncation is not considered exceptional
    int i = (int)42.5; // value becomes 42, no diagnostic
}
```

Control flow analysis ensures these conversions are only reported when reachable on actual paths.

---

## Special cases

### Expression‑bodied property declarations (Core analysis)

Normally, `[Throws]` belongs on accessors:

```c#
public int TestProp 
{
    [Throws(typeof(InvalidOperationException))]
    get => throw new InvalidOperationException();
}
```

But with expression‑bodied properties, `[Throws]` applies to the property itself:

```c#
[Throws(typeof(InvalidOperationException))]
public int TestProp => throw new InvalidOperationException();
```

This is treated as valid when there is only a `get` accessor.

> ℹ️ When exceptions are inferred from **XML documentation**, the same [property heuristics](#property-heuristics) apply to expression‑bodied properties: if only a `get` is present, exceptions are mapped to the getter; if only a `set` is present, exceptions are mapped to the setter.

### LINQ queries

The analyzer recognizes LINQ query operators whose extensions live in the `System.Linq` namespace and whose containing type name ends with `Enumerable`. Operators ending in `Queryable` are also supported. You can disable this by setting `disableLinqQueryableSupport` to `true`. `AsyncEnumerable` from the [`System.Linq.Async`](https://www.nuget.org/packages/System.Linq.Async) package is also recognized.

Async operator names suffixed with `Async`, `Await`, or `AwaitWithCancellation` are normalized to their synchronous counterparts so built-in exception knowledge applies.

See [docs/linq-support.md](linq-support.md) for examples.

---

## Configuration

Certain features can be toggled in `CheckedExceptions.settings.json`.

### Disable XML documentation interop

```json
{
  "disableXmlDocInterop": true
}
```

Disables XML doc interop, including .NET class library coverage.

### Disable control flow analysis

```json
{
  "disableControlFlowAnalysis": true
}
```

Disables the optional flow‑sensitive analysis.
When set, the analyzer will still report **unhandled exceptions** and enforce `[Throws]` contracts, but will no longer:

* Detect redundant catch clauses (`THROW009`, `THROW013`, `THROW015`)
* Report redundant exception declarations (`THROW012`, `THROW008`)
* Highlight unreachable code (IDE gray‑out support)

### Enable legacy redundancy checks

> This option enables a simplified _light mode_.

```json
{
  "disableControlFlowAnalysis": true, // prerequisite
  "enableLegacyRedundancyChecks": true
}
```

When enabled, the analyzer performs **basic redundancy checks** without relying on full control flow analysis.

It provides:

* Detection of redundant catch clauses (`THROW009`, `THROW013`, `THROW015`)

### Disable warnings for base type `Exception`

These options control whether to warn about the usage of base type `Exception`.

```json
{
    // [Throw(typeof(Exception))] won't give warning
    "disableBaseExceptionDeclaredDiagnostic": true,

    // "throw new Exception()" won't give warning
    "disableBaseExceptionThrownDiagnostic": true
}
```

> If another analyzer is used, it might warn about the use of base type `Exceptions` instead.


### Treat `[Throws(typeof(Exception))]` as catch-all

Allows `[Throws(typeof(Exception))]` to act as a catch-all for undeclared exceptions and suppresses diagnostic `THROW008` for redundant declarations covered by `System.Exception`. The base-type declaration diagnostic (`THROW003`) remains active and can be disabled via `disableBaseExceptionDeclaredDiagnostic`.

```json
{
    "treatThrowsExceptionAsCatchRest": true
}
```

This can be handy in unit tests where only assertion failures are relevant. Setup code might throw additional exceptions that you do not want to enumerate individually. Declaring `Exception` covers those extra cases while still allowing specific assertion exceptions to be declared:

```csharp
[Fact]
[Throws(typeof(NotNullException), typeof(Exception))]
public void Test()
{
    // Might throw 'FormatException' and 'OverflowException'
    var number = int.Parse("42");

    // Might throw 'NotNullException'
    Assert.NotNull(null);
}
```

With `treatThrowsExceptionAsCatchRest` enabled, `[Throws(typeof(Exception))]` suppresses diagnostics for the uninteresting `FormatException` and `OverflowException`, while the `NotNullException` declaration keeps the focus on assertion-related failures.


### Disable LINQ support

This option controls whether all analysis on LINQ constructs should be disabled.

```json
{
    "disableLinqSupport": true,
}
```

Most notably, terminator operators (such as `First`) will not indicate the exceptions they propagate:

```csharp
IEnumerable<int> items = [1, 42, 3];
var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));

// First will not indicate exceptions
var r = query.First();
```

### Disable LINQ IQueryable support

This option disables analysis of LINQ operators defined on `Queryable`. Expression tree translation depends on the provider, so disable this when your provider behaves differently.

```json
{
    "disableLinqQueryableSupport": true
}
```

#### Disable implicitly declared exceptions in lambdas

This option controls whether to disable implicitly declared exceptions in lambdas passed into LINQ operator methods. When enabled (the default), exceptions are inferred and any `[Throws]` declarations on these lambdas are reported as redundant.

```json
{
  "disableLinqImplicitlyDeclaredExceptions": false,
}
```