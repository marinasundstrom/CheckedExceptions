# ✅ Code Fix Specification

This document describes the available **code fixes** and which diagnostics they apply to.

---

## 📊 Diagnostic → Fix Summary

| Diagnostic ID | Description                           | Available Fixes                                                                                 |
| ------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------ |
| **THROW001**  | Unhandled exception type              | 🔧 Add throws declaration<br>🧯 Surround with try/catch<br>➕ Add catch clause to surrounding try<br>➕ Introduce catch clause |
| **THROW004**  | Redundant typed catch clause          | 🧹 Remove redundant catch clause                                                                 |
| **THROW005**  | Redundant exception declaration       | 🗑️ Remove redundant throws declaration                                                          |
| **THROW007**  | Missing throws from base/interface    | 🔧 Add throws declaration from base member                                                       |
| **THROW011**  | Missing throws from XML documentation | 🔧 Add throws declaration from XML doc                                                           |
| **THROW013**  | Redundant typed catch-all clause      | 🧹 Remove redundant catch clause    
| **THROW014**  | Catch clause has no remaining exceptions to handle      | 🧹 Remove redundant catch clause

---

## What is a Throwing Site?

A **throwing site** is either:

* A `throw` statement, or
* A method, property, or event access where a `[Throws(...)]`-annotated member propagates an exception.

---

## 🔧 `Add throws declaration`

**Applies to:**

* `THROW001` – *Unhandled exception type*

Adds undeclared exception types to the `[Throws]` attribute of the declaring member—either creating a new attribute or updating an existing one.

### Case: `throw` statement

```csharp
// Before
public void Foo()
{
    // THROW001: Unhandled exception type 'InvalidOperationException'
    throw new InvalidOperationException();
}

// After
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

### Case: Calling a throwing member

```csharp
// Before
public void Bar()
{
    // THROW001: Unhandled exception type 'InvalidOperationException'
    Foo();
}

[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}

// After
[Throws(typeof(InvalidOperationException))]
public void Bar()
{
    Foo();
}

[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

### Case: Property accessor

```csharp
// Before
public int Foo
{
    get => throw new InvalidOperationException();
}

// After
public int Foo
{
    [Throws(typeof(InvalidOperationException))]
    get => throw new InvalidOperationException();
}
```

### Case: Expression-bodied property

```csharp
// Before
public int Foo => throw new InvalidOperationException();

// After
[Throws(typeof(InvalidOperationException))]
public int Foo => throw new InvalidOperationException();
```

---

## 🧯 `Surround with try/catch`

**Applies to:**

* `THROW001` – *Unhandled exception type*

Wraps the throwing site (and its directly related code) in a `try`/`catch` block.

> ⚠️ **Notes:**
>
> * Not a silver bullet—manual follow-up may be required.
> * In **lambdas** or **expression-bodied members**, the expression is automatically **converted into a block-bodied form** before wrapping.

### Case: Statement

```csharp
// Before
int x = 2;
// THROW001: Unhandled exception type 'InvalidOperationException'
Foo(x);

[Throws(typeof(InvalidOperationException))]
public void Foo(int arg)
{
    throw new InvalidOperationException();
}

// After
try
{
    int x = 2;
    Foo(x);
}
catch (InvalidOperationException invalidOperationException)
{
}

[Throws(typeof(InvalidOperationException))]
public void Foo(int arg)
{
    throw new InvalidOperationException();
}
```

### Case: Lambda

```csharp
// Before
Func<int, int> f = x => Foo(x); // THROW001

// After
Func<int, int> f = x =>
{
    try
    {
        return Foo(x);
    }
    catch (InvalidOperationException invalidOperationException)
    {
        return default;
    }
};
```

---

## ➕ `Add catch clause to surrounding try`

**Applies to:**

* `THROW001` – *Unhandled exception type*

Adds a missing `catch` clause for a specific exception type to an **existing** surrounding `try`.

```csharp
// Before
try
{
    Foo();
    // THROW001: Unhandled exception type 'ArgumentException'
    Bar(2);
}
catch (InvalidOperationException invalidOperationException)
{
}

[Throws(typeof(InvalidOperationException))]
public void Foo() { /* ... */ }

[Throws(typeof(ArgumentException))]
public void Bar(int arg) { /* ... */ }

// After
try
{
    Foo();
    Bar(2);
}
catch (InvalidOperationException invalidOperationException)
{
}
catch (ArgumentException argumentException)
{
}

[Throws(typeof(InvalidOperationException))]
public void Foo() { /* ... */ }

[Throws(typeof(ArgumentException))]
public void Bar(int arg) { /* ... */ }
```

---

## ➕ `Introduce catch clauses` (for rethrown exceptions)

**Applies to:**

* `THROW001` – *Unhandled exception type*

Prepends `catch` clause for a rethrown exception type in catch all.

```csharp
// Before
try
{
    // THROW001: Unhandled exception type 'InvalidOperationException'
    Foo();
    // THROW001: Unhandled exception type 'ArgumentException'
    Bar(2);
}
catch
{
    // THROW001: Unhandled exception type 'InvalidOperationException'
    // THROW001: Unhandled exception type 'ArgumentException'
    throw;
}

[Throws(typeof(InvalidOperationException))]
public void Foo() { /* ... */ }

[Throws(typeof(ArgumentException))]
public void Bar(int arg) { /* ... */ }

// After
try
{
    Foo();
    Bar(2);
}
catch (InvalidOperationException invalidOperationException)
{
}
catch (ArgumentException argumentException)
{
}
catch // This is left intentionally
{
    throw;
}

[Throws(typeof(InvalidOperationException))]
public void Foo() { /* ... */ }

[Throws(typeof(ArgumentException))]
public void Bar(int arg) { /* ... */ }
```

---

## 🧹 `Remove redundant catch clause`

**Applies to:**

* `THROW004` – *Redundant typed catch clause*
* `THROW013` – *Redundant catch-all clause*
* `THROW014` – *Catch clause has no remaining exceptions to handle*

Removes a redundant `catch` clause for an exception type that is **not thrown** in the current `try` block.

> ⚠️ **Notes:**
>
> * In **lambdas** or **expression-bodied members**, removing the last `catch` restores expression form.
> * In **statements**, removing the last `catch` removes the entire `try` and inlines the contents.

### Case: Statement

```csharp
// Before
try
{
    Foo();
}
catch (InvalidOperationException invalidOperationException)
{
}
catch (ArgumentException argumentException) // redundant
{
}

[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}

// After
try
{
    Foo();
}
catch (InvalidOperationException invalidOperationException)
{
}

[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

### Case: Lambda

```csharp
// Before
Func<int, int> f = x =>
{
    try
    {
        return x;
    }
    catch (ArgumentException argumentException) { } // redundant
};

// After
Func<int, int> f = x => x;
```

---

## 🗑️ `Remove redundant throws declaration`

**Applies to:**

* `THROW005` – *Redundant exception declaration*

Removes a `[Throws]` declaration for an exception type that is **never thrown**.

```csharp
// Before
[Throws(typeof(InvalidDataException))] // redundant
public bool Foo2 => true;

// After
public bool Foo2 => true;
```

---

## 🔧 `Add throws declaration from base member`

**Applies to:**

* `THROW007` – *Missing throws from base/interface member*

Adds `[Throws]` attributes so that an overriding or implementing member is compatible with its base or interface contract.

### Case: Method

```csharp
// Before
public interface IFoo
{
    [Throws(typeof(InvalidOperationException))]
    void Bar();
}

public class Foo : IFoo
{
    public void Bar() { throw new InvalidOperationException(); } // THROW007
}

// After
public interface IFoo
{
    [Throws(typeof(InvalidOperationException))]
    void Bar();
}

public class Foo : IFoo
{
    [Throws(typeof(InvalidOperationException))]
    public void Bar() { throw new InvalidOperationException(); }
}
```

### Case: Property accessor

```csharp
// Before
public abstract class Base
{
    [Throws(typeof(ArgumentException))]
    public abstract int Value { get; }
}

public class Derived : Base
{
    public override int Value => throw new ArgumentException(); // THROW007
}

// After
public abstract class Base
{
    [Throws(typeof(ArgumentException))]
    public abstract int Value { get; }
}

public class Derived : Base
{
    [Throws(typeof(ArgumentException))]
    public override int Value => throw new ArgumentException();
}
```

---

## 🔧 `Add throws declaration from XML doc`

**Applies to:**

* `THROW011` – *Missing throws from XML documentation*

Adds `[Throws]` attributes based on `<exception>` tags in XML documentation.

### Case: Method

```csharp
// Before
/// <exception cref="ArgumentException">Thrown when input is invalid.</exception>
public void Baz() // THROW011
{
    throw new ArgumentException();
}

// After
/// <exception cref="ArgumentException">Thrown when input is invalid.</exception>
[Throws(typeof(ArgumentException))]
public void Baz()
{
    throw new ArgumentException();
}
```

### Case: Property accessor

XML documentation comments are placed on the **property declaration**, not on individual `get`/`set` accessors.
The analyzer uses **keyword heuristics** in the `<exception>` description to decide which accessor the exception belongs to:

* If the text mentions **“get”**, **“gets”**, **“getting”**, or **“retrieved”** → `[Throws]` is applied to the **getter**.
* If the text mentions **“set”**, **“sets”**, or **“setting”** → `[Throws]` is applied to the **setter**.
* If **no keywords are found**:

  * If the property has only a **getter** or only a **setter**, the `[Throws]` applies there.
  * If the property has both, the analyzer **defaults to the getter**.

#### Example: Getter

```csharp
// Before
/// <exception cref="InvalidOperationException">
/// Thrown when getting the value before initialization.
/// </exception>
public int Count // THROW011 (on get accessor)
{
    get => throw new InvalidOperationException();
}

// After
/// <exception cref="InvalidOperationException">
/// Thrown when getting the value before initialization.
/// </exception>
public int Count
{
    [Throws(typeof(InvalidOperationException))]
    get => throw new InvalidOperationException();
}
```

#### Example: Setter

```csharp
// Before
/// <exception cref="ArgumentException">
/// Thrown when setting a negative value.
/// </exception>
public int Capacity // THROW011 (on set accessor)
{
    set
    {
        if (value < 0)
            throw new ArgumentException();
    }
}

// After
/// <exception cref="ArgumentException">
/// Thrown when setting a negative value.
/// </exception>
public int Capacity
{
    [Throws(typeof(ArgumentException))]
    set
    {
        if (value < 0)
            throw new ArgumentException();
    }
}
```

#### Example: Ambiguous case (defaults to getter)

```csharp
// Before
/// <exception cref="IOException">
/// Thrown if the underlying stream is closed.
/// </exception>
public string Data // THROW011 (defaults to get)
{
    get => throw new IOException();
    set => throw new IOException();
}

// After
/// <exception cref="IOException">
/// Thrown if the underlying stream is closed.
/// </exception>
public string Data
{
    [Throws(typeof(IOException))]
    get => throw new IOException();
    set => throw new IOException();
}
```