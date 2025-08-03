# Analyzer Specification

## `Throws` attribute

The `[Throws]` attribute is the contract that tells that a method, property accessor, local function, or lambda expression might throw one or more specified exceptions.

Placing a `Throws` declaration on a declaration will mark the specified exception types as *handled* at the call site.

---

## Unhandled exceptions

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

## Handling exceptions

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

## Redundant or invalid handling

### Redundant catch clauses

* **Typed catch never matched** → **`THROW009`**

  ```c#
  try { } 
  // THROW009: Exception type 'InvalidOperationException' is never thrown
  catch (InvalidOperationException) { }
  ```

* **Catch‑all with nothing thrown** → **`THROW013`**

  ```c#
  try { } 
  // THROW013: This catch-all clause is redundant because no exceptions remain to be handled
  catch { }
  ```

### Redundant exception declarations

* **Declared but never thrown** → **`THROW012`**

  ```c#
  [Throws(typeof(InvalidOperationException))] // THROW012
  public void Foo() { }
  ```

* **Duplicate declarations** → **`THROW005`**

* **Already covered by base type** → **`THROW008`**

Flow analysis is used to determine whether declared exceptions are necessary based on actual code paths.

### Invalid placement

* **Throws on full property (instead of accessor)** → **`THROW010`**

---

## Bad practices with `Exception`

* **Throwing `System.Exception` directly** → **`THROW004`**
* **Declaring `[Throws(typeof(Exception))]`** → **`THROW003`**

---

## Inheritance hierarchies

The analyzer ensures consistency across inheritance and interface implementations:

* **Missing exceptions from base/interface** → **`THROW007`**
* **Declaring incompatible exceptions** → **`THROW006`**

Redundant handling is also reported when more general types make specific ones unnecessary (**`THROW008`**).

---

## Interop: XML documentation support

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

## Ignored exceptions

You can configure ignored exception types in `CheckedExceptions.settings.json`.

Ignored exceptions will not produce *unhandled* diagnostics, but are still reported for awareness:

* **Ignored exception propagated** → **`THROW002`**

---

## Casts and conversions

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

Flow analysis considers these conversions part of the potential throw set for a method or block.

---

## Special cases

### Expression-bodied property declarations

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
