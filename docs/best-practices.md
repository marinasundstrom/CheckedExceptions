# Exception Handling: Best Practices

This document outlines best practices for managing exceptions in your C# code, emphasizing clarity, performance, and maintainability. These principles are designed to work in tandem with the CheckedExceptions Analyzer.

---

## Table of Contents

1. [Understand What a Bug Is](#understand-what-a-bug-is)
2. [What Exceptions Represent](#what-exceptions-represent)
3. [When to Avoid Exceptions](#when-to-avoid-exceptions)
4. [How to Handle Exceptions Well](#how-to-handle-exceptions-well)
5. [Limiting and Containing Exceptions](#limiting-and-containing-exceptions)
6. [Exception Type Guidelines](#exception-type-guidelines)
7. [Alternatives to Exceptions](#alternatives-to-exceptions)
8. [Conclusion](#conclusion)

---

## Understand What a Bug Is

A **bug** is unexpected behavior. It defies user or developer expectations and can surface at any stage in development or production.

> **Example:**
>
> ```csharp
> public void Foo()
> {
>     throw new InvalidOperationException(); // Unhandled => Bug
> }
> ```

Unhandled exceptions are **bugs**. Every exception that escapes a method and isn't accounted for should be treated as a failure to anticipate or contain an error condition.

---

## What Exceptions Represent

Exceptions are not just errors—they are **exceptional**. They signal that something **unexpected or unlikely** has happened that can't be handled by regular logic.

Examples include:

* Hardware faults (e.g., I/O errors, memory access violations)
* Arithmetic overflows
* System limitations

> **Note:** Exceptions differ from expected domain errors, such as validation failures, which are better modeled using `Result` objects or alternative mechanisms.

---

## When to Avoid Exceptions

### 1. Avoid Declaring Exceptions

Use `ThrowsAttribute` if needed, but avoid **over-declaring** exceptions—especially in public APIs. It communicates unpredictability and complexity to callers.

### 2. Avoid Using Exceptions for Control Flow

Use non-throwing APIs when available:

> **Preferred:**
>
> ```csharp
> if (int.TryParse(input, out var result)) { ... }
> ```
>
> **Avoid:**
>
> ```csharp
> int result = int.Parse(input); // Throws on failure
> ```

---

## How to Handle Exceptions Well

### 1. Catch Exceptions Close to the Source

Catching exceptions near where they are thrown reduces the cost of stack unwinding and improves clarity.

> **Example:**
>
> ```csharp
> try
> {
>     SaveToDisk();
> }
> catch (IOException ex)
> {
>     LogError(ex);
>     Retry();
> }
> ```

### 2. Avoid Top-Level Catch-Alls

Catching all exceptions globally hides bugs and encourages ignoring faults.

> **Anti-pattern:**
>
> ```csharp
> try
> {
>     RunApplication();
> }
> catch (Exception)
> {
>     // Swallowed! Don't do this unless you're logging & exiting
> }
> ```

---

## Limiting and Containing Exceptions

### 1. Avoid Throwing More Than Four Exception Types per Method

Too many exceptions make it hard to reason about possible failure paths.

### 2. Catch and Wrap as Domain Exception

Aggregate multiple exception scenarios under a clearly named `DomainException`:

> ```csharp
> catch (IOException ex) when (ex.Message.Contains("disk"))
> {
>     throw new StorageUnavailableException("Disk error", ex);
> }
> ```

Still, prefer `Result` objects for recoverable, expected issues.

---

## Exception Type Guidelines

### ✅ Reuse .NET Framework Exceptions:

* `InvalidOperationException`
* `ArgumentNullException`
* `ArgumentOutOfRangeException`

These are conventional and understood by most developers.

### 🚫 Avoid Reusing Internal/System Exceptions:

Do **not** throw:

* `NullReferenceException`
* `StackOverflowException`
* `AccessViolationException`

These are meant for the runtime.

### 🚫 Avoid AggregateException

Use a custom, descriptive wrapper instead. `AggregateException` is difficult to pattern match and leads to boilerplate.

### 🚫 Avoid Exception Type Hierarchies

Stick to **flat**, explicitly named exception classes when custom exceptions are needed.

> **Avoid:**
>
> ```csharp
> public class MyBaseException : Exception { }
> public class FooException : MyBaseException { }
> public class BarException : MyBaseException { }
> ```
>
> **Prefer:**
>
> ```csharp
> public class UserAccountLockedException : Exception { }
> ```

### 🚫 Avoid Catching Base Type `Exception`

Too broad and may mask unexpected issues:

> ```csharp
> catch (Exception) { ... } // Try to avoid
> ```

---

## Alternatives to Exceptions

### 1. Prefer Result Objects for Domain Logic

> **Example:**
>
> ```csharp
> public record Result(bool Success, string? Error);
>
> public Result RegisterUser(string name)
> {
>     if (string.IsNullOrWhiteSpace(name))
>         return new(false, "Name is required");
>
>     return new(true, null);
> }
> ```

### 2. Nullable Reference Types

Leverage the compiler’s nullable analysis to prevent `ArgumentNullException`s.

---

## Conclusion

Exceptions should remain what they were intended to be—**exceptional**. By minimizing their use, limiting their propagation, and favoring explicit, predictable error signaling mechanisms like `Result`, you write code that’s easier to maintain, safer to extend, and more aligned with user expectations.

Together with the **CheckedExceptions Analyzer**, these practices help ensure that exceptions remain manageable and that your software becomes less exception-prone and less buggy.
