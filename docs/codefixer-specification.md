# ✅ Code Fixer Specification

The following sections describe the available code fixers for **throwing sites**—locations where the `THROW001` diagnostic is reported.

A **throwing site** is either:

* A `throw` statement,
* A method/property/event access where a `[Throws(...)]`-annotated member propagates an exception.

---

## 🔧 `Add throws declaration`

Adds undeclared exception types to the `[Throws]` attribute of the declaring member—either creating or updating the attribute.

### Case: `throw` statement

**Before:**

```csharp
public void Foo()
{
    // THROW001: Unhandled exception type 'InvalidOperationException'
    throw new InvalidOperationException();
}
```

**After:**

```csharp
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

---

### Case: Calling a throwing member

This includes calls to methods, property accessors, or event invocations.

**Before:**

```csharp
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
```

**After:**

```csharp
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

---

## 🧯 `Surround with try/catch`

Wraps the throwing site and its directly related code (read/write variables) in a `try` block with an appropriate `catch` clause.

> ⚠️ **Note:**
> This fix **isn’t a silver bullet**. In existing code, it may require manual follow-up. But for new code, it provides a useful starting point for handling exceptions incrementally.

**Before:**

```csharp
int x = 2;
// THROW001: Unhandled exception type 'InvalidOperationException'
Foo(x);

[Throws(typeof(InvalidOperationException))]
public void Foo(int arg)
{
    throw new InvalidOperationException();
}
```

**After:**

```csharp
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

---

## ➕ `Add catch clause to surrounding try`

Adds a missing `catch` clause for a specific exception type to an existing surrounding `try` block.

**Before:**

```csharp
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
```

**After:**

```csharp
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

## 🧹 `Remove redundant catch clause`

Removes redundant `catch` clause for exception type that is not thrown within the current `try` block.
